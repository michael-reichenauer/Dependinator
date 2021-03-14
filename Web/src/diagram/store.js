import { timing } from "../common/timing";
import { deserializeCanvas, serializeCanvas } from "./serialization";



class Store {
    clear() {
        localStorage.clear()
    }

    read(storeName) {
        // Get canvas data from local storage.
        let canvasText = localStorage.getItem(storeName)
        if (canvasText == null) {
            return null
        }

        const canvasData = JSON.parse(canvasText)
        if (canvasData == null) {
            console.warn('Failed to parse canvas data', storeName)
            return null
        }
        return canvasData
    }

    write(canvasData, storeName) {
        // Store canvas data in local storage
        const canvasText = JSON.stringify(canvasData)
        localStorage.setItem(storeName, canvasText)
    }
}

export const store = new Store()

