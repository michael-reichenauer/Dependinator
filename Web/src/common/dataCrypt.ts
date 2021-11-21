// import timing from './timing';
import { crypt } from "./crypt";

class DataCrypt {
  async encryptText(text: string, kek: CryptoKey) {
    const data = new TextEncoder().encode(text);

    const dek = await this.generateDek(kek);
    const cipher = await crypt.encryptData(data, dek.key);
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

    const dData = await crypt.decryptData(data, dek, iv);

    const text = new TextDecoder().decode(dData);
    return text;
  }

  async encryptWithPassword(text: string, password: string) {
    // Kek handling
    const kekSalt = crypt.generateSalt();
    const kek = await this.generateKek(password, kekSalt);

    const eData = await this.encryptText(text, kek);

    return {
      eData: eData,
      kek: {
        salt: toBase64(kekSalt),
      },
    };
  }

  async decryptWithPassword(
    encryptedPacket: { eData: any; kek: { salt: string } },
    password: string
  ): Promise<string> {
    const eData = encryptedPacket.eData;

    const kekSalt = fromBase64(encryptedPacket.kek.salt);
    const kek = await this.generateKek(password, kekSalt);

    const text = await this.decryptText(eData, kek);
    return text;
  }

  async generateKek(password: string, salt: Uint8Array): Promise<CryptoKey> {
    return await crypt.deriveKey(password, salt, [
      "encrypt",
      "wrapKey",
      "decrypt",
      "unwrapKey",
    ]);
  }

  async generateDek(
    kek: CryptoKey
  ): Promise<{ key: CryptoKey; wDek: { key: string; iv: string } }> {
    const dek = await crypt.generateKey();

    const wIv = crypt.generateIv();
    const wKey = await crypt.wrapKey(dek, kek, wIv);

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
    return await crypt.unWrapKey(wrappedDek, kek, wrappedDekIv);
  }
}

export const dataCrypt = new DataCrypt();

const toBase64 = (buffer: any) =>
  // @ts-ignore
  btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)));

const fromBase64 = (b64Text: string) => Buffer.from(b64Text, "base64");
