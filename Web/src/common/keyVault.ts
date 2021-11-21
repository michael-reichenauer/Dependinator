class KeyVault {
  kek: any = null;
  token: string | null = null;

  getToken = () => this.token;
  setToken = (token: string | null) => (this.token = token);

  getKek() {
    return this.kek;
  }

  storeKek(kek: any) {
    this.kek = kek;
  }
}

export const keyVault = new KeyVault();
