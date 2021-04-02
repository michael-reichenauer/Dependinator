
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

    async getAllDiagramsInfos() {
        return this.get(`/api/GetAllDiagramInfos`);
    }

    async newDiagram(diagramId, name, canvasData) {
        return this.post('/api/NewDiagram', {
            diagramId: diagramId,
            name: name,
            canvasData: canvasData
        });
    }

    async setCanvas(canvasData) {
        return this.post('/api/SetCanvas', canvasData);
    }

    async getDiagram(diagramId) {
        return this.get(`/api/GetDiagram?diagramId=${diagramId}`)
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