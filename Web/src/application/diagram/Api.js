
import axios from 'axios';
import { timing } from '../../common/timing';
import { atom, useAtom } from "jotai"
import { setErrorMessage, setSuccessMessage } from '../../common/MessageSnackbar';

const connectionAtom = atom(false)
let setConnectionFunc = null
let isConnectionOK = null
let isFirstCheck = true

const setConnection = flag => setConnectionFunc?.(flag)

export const useConnection = () => {
    const [connection, setConnection] = useAtom(connectionAtom)
    if (!setConnectionFunc) {
        setConnectionFunc = setConnection
    }
    return [connection]
}


export default class Api {
    token = null
    onInvalidToken = null


    setToken(token, onInvalidToken) {
        this.token = token
        this.onInvalidToken = onInvalidToken
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

    async getManifest() {
        return this.get('/manifest.json')
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
        this.handleRequest('get', uri)
        const t = timing()
        try {
            const rsp = (await axios.get(uri, { headers: { xtoken: this.token } })).data;
            this.handleOK('get', uri, null, rsp)
            return rsp
        } catch (error) {
            this.handleError('get', error, uri)
            throw (error)
        }
        finally {
            t.log('get', uri)
        }
    }

    async post(uri, data) {
        this.handleRequest('post', uri, data)
        const t = timing()
        try {
            const rsp = (await axios.post(uri, data, { headers: { xtoken: this.token } })).data;
            this.handleOK('post', uri, data, rsp)
            return rsp
        } catch (error) {
            this.handleError('post', error, uri, data)
            throw (error)
        }
        finally {
            t.log('post', uri)
        }
    }

    handleRequest(method, uri, postData) {
        console.log('Request:', method, uri, postData)
    }

    handleOK(method, uri, postData, rsp) {
        if (isConnectionOK !== true) {
            console.log('Connection OK')
            setConnection(true)
            if (!isFirstCheck) {
                setSuccessMessage('Cloud connection OK')
            }
        }
        isFirstCheck = false
        isConnectionOK = true
        console.log('OK:', method, uri, postData, rsp)
    }

    handleError(method, error, uri, postData) {
        //console.log('Failed:', method, uri, postData, error)
        if (error.response) {
            // Request made and server responded
            if (error.response.status === 500 && error.response.data?.includes('(ECONNREFUSED)')) {
                this.handleNetworkError()
                return
            }
            if (error.response.status === 400 && error.response.data?.includes('The table specified does not exist')) {
                this.handleInvalidToken()
                return
            }
            if (error.response.status === 400 && error.response.data?.includes('Invalid token')) {
                this.handleInvalidToken()
                return
            }
            console.log(`Failed ${method} ${uri} ${postData}, status: ${error.response.status}: '${error.response.data}'`)
        } else if (error.request) {
            // The request was made but no response was received
            this.handleNetworkError()
        } else {
            // Something happened in setting up the request that triggered an Error
            console.error('Error', method, uri, postData, error, error.message);
        }
    }

    handleInvalidToken() {
        console.error('Invalid Token')
        this.token = null
        this?.onInvalidToken?.()
    }

    handleNetworkError() {
        console.log('Network error')
        if (isConnectionOK !== false) {
            console.log('Connection error')
            setConnection(false)
            setErrorMessage('Cloud connection failed')
        }
        isConnectionOK = false
    }
}