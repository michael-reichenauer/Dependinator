
import axios from 'axios';
import { timing } from '../../common/timing';


export default class Api {
    token = null


    setToken(token) {
        this.token = token
    }


    async getCurrentUser() {
        console.log('host', window.location.hostname)
        if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
            await this.get('/api/Check')
            return {
                clientPrincipal: {
                    "identityProvider": "Local",
                    "userId": 'local',
                    "userDetails": 'local',
                    "userRoles": ["anonymous", "authenticated"]
                }
            }
        }

        return await this.get('/.auth/me')
    }


    async check() {
        return this.get('/api/Check')
    }

    async clearAllData() {
        return this.post('/api/ClearAllData')
    }

    async connect() {
        return this.get('/api/Connect')
    }

    async getAllDiagramsData() {
        return this.get(`/api/GetAllDiagramsData`);
    }

    async newDiagram(diagram) {
        return this.post('/api/NewDiagram', diagram);
    }

    async setCanvas(canvas) {
        return this.post('/api/SetCanvas', canvas);
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
        console.log('get', uri)
        const t = timing()
        try {
            const rsp = (await axios.get(uri, { headers: { xtoken: this.token } })).data;
            t.log('got', uri, rsp)
            return rsp
        } catch (error) {
            t.log('Failed get:', uri, error)
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
            throw (error)
        }
    }

    async post(uri, data) {
        console.log('post', uri, data)
        const t = timing()
        try {
            const rsp = (await axios.post(uri, data, { headers: { xtoken: this.token } })).data;
            t.log('posted', uri, data, rsp)
            return rsp
        } catch (error) {
            t.log('Failed post:', uri, data, error)
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
            throw (error)
        }
    }
}