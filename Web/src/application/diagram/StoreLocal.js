const diagramKey = 'diagram'
const diagramDataKey = 'DiagramData'
const lastUsedDiagramKey = 'lastUsedDiagram'


export default class StoreLocal {

    canvasKey = (diagramId, canvasId) => `${diagramKey}.${diagramId}.${canvasId}`
    diagramKey = (diagramId) => `${diagramKey}.${diagramId}.${diagramDataKey}`


    writeLastUsedDiagram(diagramId) {
        this.writeData(lastUsedDiagramKey, { diagramId: diagramId })
    }

    readLastUsedDiagramId() {
        return this.readData(lastUsedDiagramKey)?.diagramId
    }

    clearLastUsedDiagram() {
        return this.removeData(lastUsedDiagramKey)
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
                if (id === diagramId && name !== diagramDataKey) {
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
            if (key.endsWith(diagramDataKey)) {
                const value = JSON.parse(localStorage[key])
                diagrams.push({ diagramId: value.diagramId, name: value.name, accessed: value.accessed })
            }
        }

        return diagrams
    }

    updateAccessedDiagram(diagramId) {
        this.updateDiagramData(diagramId, { accessed: Date.now() })
    }


    writeDiagram(diagram) {
        this.writeDiagramData(diagram.diagramData)
        diagram.canvases.forEach(canvasData => this.writeCanvas(canvasData))
    }

    readDiagram(diagramId) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            return
        }
        const canvases = this.readCanvases(diagramId)
        const diagram = { diagramData: diagramData, canvases: canvases }

        return diagram
    }


    readDiagramData(diagramId) {
        return this.readData(this.diagramKey(diagramId))
    }

    writeDiagramData(diagramData) {
        this.writeData(this.diagramKey(diagramData.diagramId), diagramData)
    }

    updateDiagramData(diagramId, data) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            return
        }
        this.writeDiagramData({ ...diagramData, ...data })
    }

    readCanvas(diagramId, canvasId) {
        return this.readData(this.canvasKey(diagramId, canvasId))
    }

    writeCanvas(canvasData) {
        const { diagramId, canvasId } = canvasData
        this.writeData(this.canvasKey(diagramId, canvasId), canvasData)
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