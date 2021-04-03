
import axios from 'axios';


export default class Api {
    id = '12345'

    api = axios.create({
        headers: {
            common: {        // can be common or any other method
                xtoken: this.id
            }
        }
    })

    async getAllDiagramsData() {
        return this.get(`/api/GetAllDiagramsData`);
    }

    async newDiagram(diagram) {
        return this.post('/api/NewDiagram', diagram);
    }

    async setCanvas(canvasData) {
        return this.post('/api/SetCanvas', canvasData);
    }

    async getDiagram(diagramId) {
        return this.get(`/api/GetDiagram?diagramId=${diagramId}`)
    }

    async deleteDiagram(diagramId) {
        return this.post(`/api/DeleteDiagram`, { diagramId: diagramId })
    }

    async updateDiagram(diagram) {
        return this.post(`/api/UpdateDiagram`, diagram)
    }


    // api helper functions ---------------------------------
    async get(uri) {
        try {
            return (await this.api.get(uri)).data;
        } catch (error) {
            console.log(error)
            return null
        }
    }

    async post(uri, data) {
        try {
            return (await this.api.post(uri, data)).data;
        } catch (error) {
            console.log(error)
            return null
        }
    }
}