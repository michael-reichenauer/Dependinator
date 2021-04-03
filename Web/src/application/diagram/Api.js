
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

    async check() {
        return this.get('/api/Check')
    }

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

    async uploadDiagrams(diagrams) {
        return this.post(`/api/UploadDiagrams`, diagrams)
    }

    async downloadAllDiagrams() {
        return this.get(`/api/DownloadAllDiagrams`)
    }

    // api helper functions ---------------------------------
    async get(uri) {
        // console.log('get', uri)
        try {
            const rsp = (await this.api.get(uri)).data;
            // console.log('got', uri, rsp)
            return rsp
        } catch (error) {
            console.log('Failed get:', uri)
            if (error.response) {
                // Request made and server responded
                console.log(`Error: status: ${error.response.status}: '${error.response.data}'`)
            } else if (error.request) {
                // The request was made but no response was received
                console.log(`Error: request: ${error.request}: `)
            } else {
                // Something happened in setting up the request that triggered an Error
                console.log('Error', error.message);
            }
            return null
        }
    }

    async post(uri, data) {
        // console.log('post', uri, data)
        try {
            const rsp = (await this.api.post(uri, data)).data;
            // console.log('posted', uri, data, rsp)
            return rsp
        } catch (error) {
            console.log('Failed post:', uri, data)
            if (error.response) {
                // Request made and server responded
                console.log(`Error: status: ${error.response.status}: '${error.response.data}'`)
            } else if (error.request) {
                // The request was made but no response was received
                console.log(`Error: request: ${error.request}: `)
            } else {
                // Something happened in setting up the request that triggered an Error
                console.log('Error', error.message);
            }
            return null
        }
    }
}