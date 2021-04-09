import StoreFiles from "./StoreFiles"
import StoreLocal from "./StoreLocal"
import StoreSync, { rootCanvasId } from "./StoreSync"
// import { delay } from '../../common/utils'




class Store {
    files = new StoreFiles()
    local = new StoreLocal()
    sync = null

    isSyncEnabled = false

    isCloudSyncEnabled = () => this.sync.isSyncEnabled
    isLocal = () => window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'


    constructor() {
        this.sync = new StoreSync(this)
    }

    async initialize() {
        return this.sync.initialize(0)
    }

    async login(provider) {
        return this.sync.login(provider)
    }

    async disableCloudSync() {
        return this.sync.disableCloudSync()
    }

    async serverHadChanges() {
        return this.sync.serverHadChanges()
    }

    async checkCloudConnection() {
        return this.sync.checkCloudConnection()
    }

    async retryCloudConnection() {
        return this.sync.retryCloudConnection()
    }

    async openMostResentDiagramCanvas() {
        const diagramId = this.getMostResentDiagramId()
        if (!diagramId) {
            console.log('No recent diagram')
            throw new Error('No resent diagram')
        }

        return this.openDiagramRootCanvas(diagramId)
    }


    async openDiagramRootCanvas(diagramId) {
        console.log('Open diagram', diagramId)
        try {
            let canvas = await this.sync.openDiagramRootCanvas(diagramId)
            if (canvas) {
                console.log('Got diagram canvas from remote', diagramId)
                // Got diagram via cloud
                return canvas
            }

            // Local mode: read the root canvas from local store
            canvas = this.local.readCanvas(diagramId, rootCanvasId)
            if (!canvas) {
                console.log('Diagram not found in local store', diagramId)
                throw new Error('Diagram not found')
            }

            this.local.updateAccessedDiagram(canvas.diagramId)
            return canvas
        } catch (error) {
            this.local.removeDiagram(diagramId)
            throw error
        }
        finally {
            await this.sync.syncDiagrams()
        }
    }


    getUniqueSystemName() {
        const infos = this.getRecentDiagramInfos()

        for (let i = 0; i < 20; i++) {
            const name = i === 0 ? 'System' : `System (${i})`
            if (!infos.find(info => name === info.name)) {
                // No other info with that name
                return name
            }
        }

        // Seems all names are used, lets just reuse System
        return 'System'
    }

    getMostResentDiagramId() {
        return this.getRecentDiagramInfos()[0]?.diagramId
    }

    async newDiagram(diagramId, name, canvas) {
        console.log('new diagram', diagramId, name)
        const now = Date.now()
        const diagram = {
            diagramInfo: { diagramId: diagramId, name: name, accessed: now, written: now },
            canvases: [canvas]
        }
        this.local.writeDiagram(diagram)

        await this.sync.newDiagram(diagram)
    }

    setCanvas(canvas) {
        this.local.writeCanvas(canvas)
        this.local.updateWrittenDiagram(canvas.diagramId)

        this.sync.setCanvas(canvas)
    }

    async deleteDiagram(diagramId) {
        console.log('Delete diagram', diagramId)
        this.local.removeDiagram(diagramId)

        await this.sync.deleteDiagram(diagramId)
    }

    setDiagramName(diagramId, name) {
        this.local.updateDiagramInfo(diagramId, { name: name })
        this.local.updateWrittenDiagram(diagramId)


        this.sync.setDiagramName(diagramId, name)
    }

    getCanvas(diagramId, canvasId) {
        return this.local.readCanvas(diagramId, canvasId)
    }

    getRecentDiagramInfos() {
        return this.local.readAllDiagramsInfos()
            .sort((i1, i2) => i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0)
            .reverse()
    }

    async loadDiagramFromFile() {
        const file = await this.files.loadFile()

        if (!await this.sync.uploadDiagrams(file.diagrams)) {
            // save locally
            file.diagrams.forEach(diagram => this.local.writeDiagram(diagram))
        }

        const firstDiagramId = file.diagrams[0]?.diagramInfo.diagramId
        if (!firstDiagramId) {
            throw new Error('No diagram in file')
        }
        return firstDiagramId
    }

    saveDiagramToFile(diagramId) {
        const diagram = this.local.readDiagram(diagramId)
        if (diagram == null) {
            return
        }

        const file = { diagrams: [diagram] }
        this.files.saveFile(`${diagram.diagramInfo.name}.json`, file)
    }

    async saveAllDiagramsToFile() {
        let diagrams = await this.sync.downloadAllDiagrams()
        if (!diagrams) {
            // Read from local    
            diagrams = this.local.readAllDiagrams()
        }

        const file = { diagrams: diagrams }
        this.files.saveFile(`diagrams.json`, file)
    }

    clearLocalData() {
        this.local.clearAllData()
    }

    async clearRemoteData() {
        return this.sync.clearRemoteData()
    }


    // For printing 
    getDiagram(diagramId) {
        return this.local.readDiagram(diagramId)
    }
}

export const store = new Store()

