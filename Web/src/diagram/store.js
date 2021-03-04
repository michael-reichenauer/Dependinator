import { timing } from "../common/timing";
import { deserializeCanvas, serializeCanvas } from "./serialization";


export const clearStoredDiagram = () => {
    localStorage.clear()
}

export const saveDiagram = (canvas, storeName) => {
    // Serialize canvas figures and connections into canvas data object
    let t = timing()
    const canvasData = serializeCanvas(canvas);
    t.log('serialized')

    saveData(canvasData, storeName)
    t.log('saved')
}

export const saveData = (canvasData, storeName) => {
    // Serialize canvas figures and connections into canvas data object
    let t = timing()

    // Store canvas data in local storage
    const canvasText = JSON.stringify(canvasData)
    t.log('stringified')

    localStorage.setItem(storeName, canvasText)
    t.log('saved')
}

export const loadDiagram = (canvas, storeName) => {
    let t = timing()
    const canvasData = loadData(storeName)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        console.log('no diagram could be parsed (or no figures) for', storeName)
        return false
    }

    // Deserialize canvas
    deserializeCanvas(canvas, canvasData)
    t.log('deserialized')
    return true
}

export const loadData = (storeName) => {
    let t = timing()
    // Get canvas data from local storage.
    let canvasText = localStorage.getItem(storeName)
    t.log('loaded')

    if (canvasText == null) {
        console.log('no stored diagram for', storeName)
        return null
    }
    //console.log('saved', canvasText)
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        console.log('no diagram could be parsed (or no figures) for', storeName)
        return null
    }
    t.log('parsed')
    return canvasData
}
