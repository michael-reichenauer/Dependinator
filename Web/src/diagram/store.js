import { deserializeCanvas, serializeCanvas } from "./serialization";


export const saveDiagram = (canvas, storeName) => {
    // Serialize canvas figures and connections into canvas data object
    const canvasData = serializeCanvas(canvas);

    // Store canvas data in local storage
    const canvasText = JSON.stringify(canvasData)
    localStorage.setItem(storeName, canvasText)
    console.log('saved', storeName)
}

export const restoreDiagram = (canvas, storeName) => {
    // Get canvas data from local storage.
    let canvasText = localStorage.getItem(storeName)


    if (canvasText == null) {
        console.log('no stored diagram for', storeName)
        return false
    }
    //console.log('saved', canvasText)
    const canvasData = JSON.parse(canvasText)
    if (canvasData == null || canvasData.figures == null || canvasData.figures.lengths === 0) {
        console.log('no diagram could be parsed (or no figures) for', storeName)
        return false
    }

    // Deserialize canvas
    console.log('loaded', storeName)
    deserializeCanvas(canvas, canvasData)
    return true
}
