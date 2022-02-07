// import timing from './timing';

import { ICrypt, ICryptKey } from "./crypt";
import { di, diKey, singleton } from "./di";

export interface EncryptedData {}

export const IDataCryptKey = diKey<IDataCrypt>();
export interface IDataCrypt {
  encryptWithPassword(
    text: string,
    username: string,
    password: string
  ): Promise<EncryptedData>;
  decryptWithPassword(
    ed: any,
    username: string,
    password: string
  ): Promise<string>;
  generateKek(username: string, password: string): Promise<CryptoKey>;
}

@singleton(IDataCryptKey)
export class DataCrypt {
  constructor(private crypt: ICrypt = di(ICryptKey)) {}

  public async encryptWithPassword(
    text: string,
    username: string,
    password: string
  ): Promise<EncryptedData> {
    // Kek handling
    const kek = await this.generateKek(username, password);
    console.log("kek", kek);

    const eData = await this.encryptText(text, kek);
    return eData;
  }

  public async decryptWithPassword(
    eData: any,
    username: string,
    password: string
  ): Promise<string> {
    const kek = await this.generateKek(username, password);

    const text = await this.decryptText(eData, kek);

    return text;
  }

  public async generateKek(
    username: string,
    password: string
  ): Promise<CryptoKey> {
    // using hash of username as salt. Usually a salt is a random number, but in this case, it
    // is sufficient and convenient to use the username
    const salt = await this.crypt.sha256(username);
    return await this.crypt.deriveKey(password, salt, [
      "encrypt",
      "wrapKey",
      "decrypt",
      "unwrapKey",
    ]);
  }

  public async encryptText(text: string, kek: CryptoKey) {
    const dek = await this.generateDek(kek);

    const data = new TextEncoder().encode(text);

    const cipher = await this.crypt.encryptData(data, dek.key);

    return {
      data: toBase64(cipher.data),
      iv: toBase64(cipher.iv),
      wDek: dek.wDek,
    };
  }

  async decryptText(
    eData: { wDek: any; data: any; iv: any },
    kek: CryptoKey
  ): Promise<string> {
    const dek = await this.unWrapDek(eData.wDek, kek);

    const data = fromBase64(eData.data);
    const iv = fromBase64(eData.iv);

    const dData = await this.crypt.decryptData(data, dek, iv);

    const text = new TextDecoder().decode(dData);
    return text;
  }

  async generateDek(
    kek: CryptoKey
  ): Promise<{ key: CryptoKey; wDek: { key: string; iv: string } }> {
    const dek = await this.crypt.generateKey();

    const wIv = this.crypt.generateIv();
    const wKey = await this.crypt.wrapKey(dek, kek, wIv);

    return {
      key: dek,
      wDek: {
        key: toBase64(wKey),
        iv: toBase64(wIv),
      },
    };
  }

  async unWrapDek(
    wDek: { key: any; iv: any },
    kek: CryptoKey
  ): Promise<CryptoKey> {
    const wrappedDek = fromBase64(wDek.key);
    const wrappedDekIv = fromBase64(wDek.iv);
    return await this.crypt.unWrapKey(wrappedDek, kek, wrappedDekIv);
  }
}

const toBase64 = (buffer: any) =>
  // @ts-ignore
  btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)));

const fromBase64 = (b64Text: string) => Buffer.from(b64Text, "base64");
