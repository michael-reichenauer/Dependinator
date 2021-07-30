class KeyVault {
    kek = null

    getKek() {
        return this.kek
    }

    storeKek(kek) {
        this.kek = kek
    }
}

export const keyVault = new KeyVault()