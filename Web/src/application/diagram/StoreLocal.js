const diagramKey = 'diagram'
const diagramInfoKey = 'diagramInfo'
const syncKey = 'sync'


export default class StoreLocal {

    canvasKey = (diagramId, canvasId) => `${diagramKey}.${diagramId}.${canvasId}`
    diagramKey = (diagramId) => `${diagramKey}.${diagramId}.${diagramInfoKey}`

    clearAllData() {
        localStorage.clear()
    }

    getSync() {
        return this.readData(syncKey) ?? {}
    }

    updateSync(data) {
        const sync = { ...this.getSync(), ...data }
        this.writeData(syncKey, sync)
        return sync
    }


    readAllDiagrams() {
        return this.readAllDiagramsInfos()
            .map(d => this.readDiagram(d.diagramId))
            .filter(d => d != null)
    }

    readCanvases(diagramId) {
        const keys = []

        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.startsWith(diagramKey)) {
                const parts = key.split('.')
                const id = parts[1]
                const name = parts[2]
                if (id === diagramId && name !== diagramInfoKey) {
                    keys.push(key)
                }
            }
        }

        return keys.map(key => this.readData(key)).filter(data => data != null)
    }

    removeDiagram(diagramId) {
        let keys = []

        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.startsWith(diagramKey)) {
                const parts = key.split('.')
                const id = parts[1]
                if (id === diagramId) {
                    keys.push(key)
                }
            }
        }

        keys.forEach(key => this.removeData(key))
    }

    readAllDiagramsInfos() {
        const diagrams = []
        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.endsWith(diagramInfoKey)) {
                const diagramInfo = JSON.parse(localStorage[key])
                diagrams.push(diagramInfo)
            }
        }

        return diagrams
    }

    updateAccessedDiagram(diagramId) {
        this.updateDiagramInfo(diagramId, { accessed: Date.now() })
    }


    writeDiagram(diagram) {
        this.writeDiagramInfo(diagram.diagramInfo)
        diagram.canvases.forEach(canvas => this.writeCanvas(canvas))
    }

    readDiagram(diagramId) {
        const diagramInfo = this.readDiagramInfo(diagramId)
        if (diagramInfo == null) {
            return
        }
        const canvases = this.readCanvases(diagramId)
        const diagram = { diagramInfo: diagramInfo, canvases: canvases }

        return diagram
    }


    readDiagramInfo(diagramId) {
        return this.readData(this.diagramKey(diagramId))
    }

    writeDiagramInfo(diagramInfo) {
        this.writeData(this.diagramKey(diagramInfo.diagramId), diagramInfo)
    }

    updateDiagramInfo(diagramId, data) {
        const diagramInfo = this.readDiagramInfo(diagramId)
        if (diagramInfo == null) {
            return
        }
        this.writeDiagramInfo({ ...diagramInfo, ...data })
    }

    readCanvas(diagramId, canvasId) {
        return this.readData(this.canvasKey(diagramId, canvasId))
    }

    writeCanvas(canvas) {
        const { diagramId, canvasId } = canvas
        this.writeData(this.canvasKey(diagramId, canvasId), canvas)
    }

    readData(key) {
        let text = localStorage.getItem(key)
        if (text == null) {
            return null
        }
        return JSON.parse(text)
    }

    writeData(key, data) {
        const text = JSON.stringify(data)
        localStorage.setItem(key, text)
    }

    removeData(key) {
        localStorage.removeItem(key)
    }
}