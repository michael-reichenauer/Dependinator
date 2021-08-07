class KeyVault {
    kek = null
    token = null

    getToken = () => this.token
    setToken = (token) => this.token = token

    getKek() {
        return this.kek
    }

    storeKek(kek) {
        this.kek = kek
    }
}

export const keyVault = new KeyVault()