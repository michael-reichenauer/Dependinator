import { setErrorMessage, setInfoMessage, setSuccessMessage } from "../../common/MessageSnackbar"
import { setProgress } from "../../common/Progress"
import { setSyncMode } from "../Online"
import Api from "./Api"
import { keyVault } from './../../common/keyVault';

export const rootCanvasId = 'root'

export default class StoreSync {

    store = null
    local = null
    api = null

    isSyncEnabled = false

    constructor(store) {
        this.store = store
        this.local = store.local
        this.api = new Api(this.onInvalidToken)
    }

    onInvalidToken() {
        console.log('on invalid token')
        keyVault.setToken(null)
        // Called when token id invalid
        if (this.isSyncEnabled) {
            this.isSyncEnabled = false
            this.local.updateSync({ token: null, isConnecting: false, provider: null, details: null })
            setSyncMode(false)
            setErrorMessage('Cloud sync failed. You need to re-enable cloud sync')
        }
    }

    async checkCloudConnection() {
        setProgress(true)
        try {
            await this.api.check()
            setSuccessMessage('Cloud connection OK')
        } catch (error) {
            setErrorMessage('No connection with cloud server')
        } finally {
            setProgress(false)
        }
    }

    async initialize() {
        console.log('initialize')
        let sync = this.local.getSync()

        console.log('sync', sync)
        if (sync.isConnecting) {
            console.log('Connecting after a previous login')
            // A previous login triggered reload and now we should call connect
            this.local.updateSync({ isConnecting: false })
            const connectData = await this.api.connect()
            console.log('connected', connectData)
            sync = this.local.updateSync({ token: connectData.token, provider: connectData.provider, details: connectData.details })
            setSuccessMessage('Cloud sync connection is enabled')
        } else if (sync.isConnected) {
            // When using custom id
            this.local.updateSync({ isConnected: false })
            setSuccessMessage('Cloud sync connection is enabled')
        }

        if (!sync.token) {
            console.log('No sync token, sync is disabled')
            this.isSyncEnabled = false
            setSyncMode(false)
            return
        }

        keyVault.setToken(sync.token)


        this.isSyncEnabled = true
        setSyncMode(true)
        console.log('Sync is enabled')

        await this.syncDiagrams()
    }

    async serverHadChanges() {
        if (!this.isSyncEnabled) {
            return false
        }

        const before = this.store.getRecentDiagramInfos()[0]
        await this.syncDiagrams()
        const after = this.store.getRecentDiagramInfos()[0]
        if (before.diagramId === after.diagramId && before.timestamp === after.timestamp) {
            return false
        }
        console.log('Server had changes')
        return true
    }

    // async login(provider) {
    //     console.log('Login with', provider)

    //     try {
    //         // Checking if user already is logged in with the specified provider
    //         const user = await this.remote.getCurrentUser()
    //         if (user?.clientPrincipal?.identityProvider === provider) {
    //             // User is logged in, lets just reload site (no need to login again)
    //             console.log('Still logged in with', provider)
    //             window.location.reload()
    //         }

    //     } catch (error) {
    //         // Failed to check current user, lets ignore that and login
    //         console.trace('error', error)
    //     }

    //     this.local.updateSync({ isConnecting: true, provider: provider })
    //     // Login for the specified id provider
    //     if (provider === 'Google') {
    //         window.location.href = `/.auth/login/google`;
    //     } else if (provider === 'Microsoft') {
    //         window.location.href = `/.auth/login/aad`;
    //     } else if (provider === 'Facebook') {
    //         window.location.href = `/.auth/login/facebook`;
    //     } else if (provider === 'GitHub') {
    //         window.location.href = `/.auth/login/github`;
    //     } else {
    //         this.local.updateSync({ isConnecting: false, provider: null })
    //         throw new Error('Unsupported identity provider ' + provider)
    //     }
    // }

    async createUser(user) {
        await this.api.createUser(user)
    }

    async connectUser(user) {
        const connectData = await this.api.connectUser(user)
        this.local.updateSync({ isConnecting: false, isConnected: true, token: connectData.token, provider: connectData.provider, details: connectData.details })
        window.location.reload()
    }

    disableCloudSync() {
        if (!this.isSyncEnabled) {
            return
        }

        console.log('Disable cloud sync')
        this.isSyncEnabled = false
        this.local.updateSync({ token: null, isConnecting: false, provider: null })
        this.api.setToken(null, null)
        setSyncMode(false)
        setInfoMessage('Cloud sync is disabled')
    }

    async openDiagramRootCanvas(diagramId) {
        if (!this.isSyncEnabled) {
            return null
        }
        // Try to get diagram from remote server and cache locally
        const diagram = await this.api.getDiagram(diagramId)
        this.local.writeDiagram(diagram)

        try {
            // Now read the root canvas from local store
            console.log('gettin ', diagramId)
            const d = this.local.readCanvas(diagramId, rootCanvasId)
            console.log('got', d)
            return d
        } catch (error) {
            console.trace('error', error)
            throw error
        }

    }

    async newDiagram(diagram) {
        if (!this.isSyncEnabled) {
            return

        }
        // Sync with remote server
        const diagramInfo = await this.api.newDiagram(diagram)
        this.local.writeDiagramInfo(diagramInfo)
    }

    setCanvas(canvas) {
        if (!this.isSyncEnabled) {
            return
        }

        // Sync with remote server
        this.api.setCanvas(canvas)
            .then(diagramInfo => this.local.writeDiagramInfo(diagramInfo))
            .catch(error => setErrorMessage('Failed to sync canvas change'))
    }

    async deleteDiagram(diagramId) {
        if (!this.isSyncEnabled) {
            return
        }

        await this.api.deleteDiagram(diagramId)
        await this.syncDiagrams()
    }

    setDiagramName(diagramId, name) {
        if (!this.isSyncEnabled) {
            return
        }

        this.api.updateDiagram({ diagramInfo: { diagramId: diagramId, name: name } })
            .then(diagramInfo => this.local.writeDiagramInfo(diagramInfo))
            .catch(error => setErrorMessage('Failed to sync name change'))
    }

    async uploadDiagrams(diagrams) {
        if (!this.isSyncEnabled) {
            return false
        }

        // Store all read diagram
        await this.api.uploadDiagrams(diagrams)
        await this.syncDiagrams()
        return true
    }

    async downloadAllDiagrams() {
        if (!this.isSyncEnabled) {
            return null
        }
        return await this.api.downloadAllDiagrams()
    }

    async clearRemoteData() {
        setProgress(true)
        try {
            if (!this.isSyncEnabled) {
                setErrorMessage('Cloud sync not enabled, cannot clear remote data')
                return false
            }
            await this.api.clearAllData()
            return true
        } catch (error) {
            setErrorMessage('Failed to clear remote data, ' + error.message)
            return false
        } finally {
            setProgress(false)
        }
    }

    async syncDiagrams() {
        if (!this.isSyncEnabled) {
            return
        }

        console.log('Syncing')
        const diagramsToPublish = []
        let localInfos = this.local.readAllDiagramsInfos()

        // Get all remote server diagrams data and write to local store
        const remoteInfos = await this.api.getAllDiagramsData()
        for (let i = 0; i < remoteInfos.length; i++) {
            const remoteInfo = remoteInfos[i];
            const localInfo = localInfos.find(l => l.diagramId === remoteInfo.diagramId)
            if (!localInfo) {
                // The remote info is not yet downloaded
                this.local.writeDiagramInfo(remoteInfo)
            } else {
                // The remote info has previously been downloaded, comparing write times
                if (localInfo.written > remoteInfo.written) {
                    // Local info is newer, lets publish
                    const diagram = this.local.readDiagram(localInfo.diagramId)
                    if (diagram) {
                        diagramsToPublish.push(diagram)
                    }
                } else {
                    // Remote might have changed, lets update local
                    this.local.writeDiagramInfo(remoteInfo)
                }
            }
        }

        // Update local diagram infos to check if diagrams are to be deleted or published
        localInfos = this.local.readAllDiagramsInfos()

        // No check of some local diagrams can be deleted or published
        for (let i = 0; i < localInfos.length; i++) {
            const localInfo = localInfos[i];
            const remoteInfo = remoteInfos.find(r => r.diagramId === localInfo.diagramId)
            if (!remoteInfo) {
                // The local info is not a remote info, check if it can be deleted or should be published
                if (localInfo.etag) {
                    // The local info was a remote, but no longer. Lets delete local as well
                    console.log('Remove', localInfo.diagramId)
                    this.local.removeDiagram(localInfo.diagramId)
                } else {
                    // The local info never pushed, lets push to remote
                    const diagram = this.local.readDiagram(localInfo.diagramId)
                    if (diagram) {
                        diagramsToPublish.push(diagram)
                    }
                }
            }
        }

        if (diagramsToPublish.length > 0) {
            // Some diagrams should be published
            await this.api.uploadDiagrams(diagramsToPublish)
        }
    }
}