import { timing } from "../common/timing";
import { deserializeCanvas, serializeCanvas } from "./serialization";


export const clearStoredDiagram = () => {
    localStorage.clear()
}

export const saveDiagram = (canvas, storeName) => {
    // Serialize canvas figures and connections into canvas data object
    let t = timing()
    const canvasData = serializeCanvas(canvas);

    saveData(canvasData, storeName)
    t.log()
}

export const saveData = (canvasData, storeName) => {
    // Store canvas data in local storage
    const canvasText = JSON.stringify(canvasData)
    localStorage.setItem(storeName, canvasText)
}

export const loadDiagram = (canvas, storeName) => {
    let t = timing()
    const canvasData = loadData(storeName)
    if (canvasData == null) {
        return false
    }

    // Deserialize canvas
    deserializeCanvas(canvas, canvasData)
    t.log()
    return true
}

export const loadData = (storeName) => {
    // Get canvas data from local storage.
    let canvasText = localStorage.getItem(storeName)
    if (canvasText == null) {
        // console.log('no stored diagram for', storeName)
        return null
    }

    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        console.warn('no diagram could be parsed (or no figures) for', storeName)
        return null
    }
    return canvasData
}
