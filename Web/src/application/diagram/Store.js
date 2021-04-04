import Api from "./Api"
import StoreFiles from "./StoreFiles"
import StoreLocal from "./StoreLocal"
import { delay } from '../../common/utils'

const rootCanvasId = 'root'

class Store {
    files = new StoreFiles()
    local = new StoreLocal()
    remote = new Api()

    setError = null
    setProgress = null
    isCloudSync = false

    setHandlers(setError, setProgress) {
        this.setError = setError
        this.setProgress = setProgress
        this.remote.setProgressHandler(setProgress)
    }

    isCloudSyncEnabled() {
        return this.isCloudSync
    }

    async enableCloudSync() {
        this.setProgress(true)
        try {
            await delay(3000)
            this.isCloudSync = true
        } catch (error) {
            this.setError('Failed to enable cloud sync')
        } finally {
            this.setProgress(false)
        }
    }

    async disableCloudSync() {
        this.setProgress(true)
        try {
            console.log('Disable sync')
            await delay(3000)
            this.isCloudSync = false
        } catch (error) {
            this.setError('Failed to disable cloud sync')
        } finally {
            this.setProgress(false)
        }
    }

    async openLastUsedDiagramCanvas() {
        const diagramId = this.local.readLastUsedDiagramId()
        if (!diagramId) {
            return null
        }

        return this.openDiagramRootCanvas(diagramId)
    }

    async openFirstDiagramRootCanvas() {
        const diagramId = this.getRecentDiagramInfos()[0]?.diagramId
        if (!diagramId) {
            return null
        }
        return this.openDiagramRootCanvas(diagramId)
    }

    async openDiagramRootCanvas(diagramId) {
        try {
            // Try to get diagram from remote server
            const diagram = await this.remote.getDiagram(diagramId)

            if (!diagram || diagram.canvases.length === 0) {
                // Removed from server, lets remove local as well (client will create new diagram)
                this.local.removeDiagram(diagramId)
                this.syncDiagrams()
                return null
            }

            // Cache diagram locally
            this.local.writeDiagram(diagram)

            // Now read the root canvas from local store
            this.local.writeLastUsedDiagram(diagramId)
            const canvasData = this.local.readCanvas(diagramId, rootCanvasId)

            await this.syncDiagrams()
            return canvasData
        } catch (error) {
            this.setError('Failed to open diagram')
            return null
        }
    }


    async newDiagram(diagramId, name, canvasData) {
        const diagram = {
            diagramData: { diagramId: diagramId, name: name, accessed: Date.now() },
            canvases: [canvasData]
        }
        this.local.writeDiagram(diagram)
        this.local.writeLastUsedDiagram(diagramId)

        // Sync with remote server
        const diagramData = await this.remote.newDiagram(diagram)
        this.local.writeDiagramData(diagramData)
    }

    setCanvas(canvasData) {
        this.local.writeCanvas(canvasData)
        this.local.updateAccessedDiagram(canvasData.diagramId)
        this.local.writeLastUsedDiagram(canvasData.diagramId)

        // Sync with remote server
        this.remote.setCanvas(canvasData)
            .then(diagramData => this.local.writeDiagramData(diagramData))
    }

    async deleteDiagram(diagramId) {
        this.local.removeDiagram(diagramId)
        this.local.clearLastUsedDiagram(diagramId)

        console.log('Delete', diagramId)
        await this.remote.deleteDiagram(diagramId)
        await this.syncDiagrams()
    }

    setDiagramName(diagramId, name) {
        this.local.updateDiagramData(diagramId, { name: name })

        this.remote.updateDiagram({ diagramData: { diagramId: diagramId, name: name } })
            .then(diagramData => this.local.writeDiagramData(diagramData))
    }

    getCanvas(diagramId, canvasId) {
        return this.local.readCanvas(diagramId, canvasId)
    }

    getRecentDiagramInfos() {
        const lastUsedDiagramId = this.local.readLastUsedDiagramId()
        return this.local.readAllDiagramsInfos()
            .filter(d => d.diagramId !== lastUsedDiagramId)
            .sort((i1, i2) => i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0)
            .reverse()
    }

    async loadDiagramFromFile() {
        const file = await this.files.loadFile()

        // Store all read diagram
        await this.remote.uploadDiagrams(file.diagrams)
        //file.diagrams.forEach(diagram => this.local.writeDiagram(diagram))

        this.syncDiagrams()
        let firstDiagramId = file.diagrams[0]?.diagramData.diagramId
        return firstDiagramId
    }

    saveDiagramToFile(diagramId) {
        const diagram = this.local.readDiagram(diagramId)
        if (diagram == null) {
            return
        }

        const file = { diagrams: [diagram] }
        this.files.saveFile(`${diagram.diagramData.name}.json`, file)
    }

    async saveAllDiagramsToFile() {
        const diagrams = await this.remote.downloadAllDiagrams()
        if (diagrams.length === 0) {
            return
        }

        const file = { diagrams: diagrams }
        this.files.saveFile(`diagrams.json`, file)
    }


    async syncDiagrams() {
        // Get all remote server diagrams data and write to local store
        const remoteInfos = await this.remote.getAllDiagramsData()
        remoteInfos.forEach(data => this.local.writeDiagramData(data))

        // Get local diagram infos to check if to be deleted or published
        const localInfos = this.local.readAllDiagramsInfos()
        const currentId = this.local.readLastUsedDiagramId()

        for (let i = 0; i < localInfos.length; i++) {
            const localInfo = localInfos[i];
            const isRemote = remoteInfos.find(remoteInfo => remoteInfo.diagramId === localInfo.diagramId)
            if (!isRemote) {
                // The local info is not a remote info
                console.log('local', localInfo)
                if (localInfo.etag) {
                    // The local info was a remote, but no longer
                    if (currentId === localInfo.diagramId) {
                        // Is the current diagram, lets re-add the diagram to remote
                        const diagram = this.local.readDiagram(localInfo.diagramId)
                        if (diagram) {
                            console.log('push', diagram)
                            const newInfo = await this.remote.newDiagram(diagram)

                            // Update local diagram info
                            this.local.writeDiagramData(newInfo)
                        }
                    } else {
                        // Local info can just be deleted since it was deleted on the server
                        console.log('Remove', localInfo.diagramId)
                        this.local.removeDiagram(localInfo.diagramId)
                    }
                } else {
                    // The local info never pushed, lets push to remote
                    const diagram = this.local.readDiagram(localInfo.diagramId)
                    if (diagram) {
                        console.log('push', diagram)
                        const newInfo = await this.remote.newDiagram(diagram)

                        // Update local diagram info
                        this.local.writeDiagramData(newInfo)
                    }
                }
            }
        }
    }

    // For printing 
    getDiagram(diagramId) {
        return this.local.readDiagram(diagramId)
    }
}

export const store = new Store()

