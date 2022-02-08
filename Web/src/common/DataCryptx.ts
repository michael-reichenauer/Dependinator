import { ICrypt, ICryptKey } from "./crypt";
import { di, diKey, singleton } from "./di";
import { User } from "./Api";

export const IDataCryptKey = diKey<IDataCrypt>();
export interface IDataCrypt {
  // Creates a key encryption key (KEK) to encrypt each unique data encryption key (DEK), which is
  // used to encrypt each encrypted text block
  deriveKeyEncryptionKey(user: User): Promise<CryptoKey>;
  // Encrypt a text block by first creating a unique data encryption key (DEK), which is then
  // encrypted/wrapped by the key encryption key (KEK)
  encryptText(text: string, keyEncryptionKey: CryptoKey): Promise<string>;
  // Decrypts a text block by first decrypt/unwrap the unique data encryption key (DEK) using the
  // provided key encryption key (KEK)
  decryptText(
    encryptedText: string,
    keyEncryptionKey: CryptoKey
  ): Promise<string>;
}

type WrappedDek = { key: any; iv: any };
type EncryptedPacket = { data: string; iv: string; wDek: WrappedDek };

@singleton(IDataCryptKey)
export class DataCrypt {
  constructor(private crypt: ICrypt = di(ICryptKey)) {}

  public async deriveKeyEncryptionKey(user: User): Promise<CryptoKey> {
    // using hash of username as salt. Usually a salt is a random number, but in this case, it
    // is sufficient and convenient to use the username as salt
    const salt = await this.crypt.sha256(user.username);
    return await this.crypt.deriveKey(user.password, salt, [
      "wrapKey",
      "unwrapKey",
    ]);
  }

  public async encryptText(
    text: string,
    keyEncryptionKey: CryptoKey
  ): Promise<string> {
    // The new unique key (DEK) to encrypt the text block
    const dek = await this.crypt.generateKey();

    // Encrypt the text
    const textBytes = new TextEncoder().encode(text);
    const cipher = await this.crypt.encryptData(textBytes, dek);

    // encrypt/wrap the DEK using the key encryption key (KEK)
    const wrappedDek = await this.wrapDataEncryptionKey(dek, keyEncryptionKey);

    // Pack the encrypted text and encrypted DEK key into a text package
    const encryptedPacket: EncryptedPacket = {
      data: toBase64(cipher.data),
      iv: toBase64(cipher.iv),
      wDek: wrappedDek,
    };
    const json = JSON.stringify(encryptedPacket);

    return utf8_to_b64(json);
  }

  public async decryptText(
    encryptedText: string,
    keyEncryptionKey: CryptoKey
  ): Promise<string> {
    // Extract the encrypted text and encrypted DEK key
    const json = b64_to_utf8(encryptedText);
    const encryptedPacket: EncryptedPacket = JSON.parse(json);

    // Decrypt/unwrap the DEK key using the KEK key
    const dek = await this.unWrapDataEncryptionKey(
      encryptedPacket.wDek,
      keyEncryptionKey
    );
    const iv = fromBase64(encryptedPacket.iv);

    // Decrypt the encrypted text using the DEK key
    const data = fromBase64(encryptedPacket.data);
    const decryptedData = await this.crypt.decryptData(data, iv, dek);

    const decryptedText = new TextDecoder().decode(decryptedData);
    return decryptedText;
  }

  private async wrapDataEncryptionKey(
    dek: CryptoKey,
    kek: CryptoKey
  ): Promise<WrappedDek> {
    // Encrypt/wrap the DEK key using the KEK key
    const wrappedDecIv = this.crypt.generateIv();
    const wrappedDekKey = await this.crypt.wrapKey(dek, wrappedDecIv, kek);

    return {
      key: toBase64(wrappedDekKey),
      iv: toBase64(wrappedDecIv),
    };
  }

  private async unWrapDataEncryptionKey(
    wrappedDek: WrappedDek,
    kek: CryptoKey
  ): Promise<CryptoKey> {
    // Decrypt/unwrap DEK key using the KEK key
    const wrappedDekKey = fromBase64(wrappedDek.key);
    const wrappedDekIv = fromBase64(wrappedDek.iv);
    return await this.crypt.unWrapKey(wrappedDekKey, wrappedDekIv, kek);
  }
}

function utf8_to_b64(str: string): string {
  // @ts-ignore
  return window.btoa(unescape(encodeURIComponent(str)));
}

function b64_to_utf8(str: string): string {
  // @ts-ignore
  return decodeURIComponent(escape(window.atob(str)));
}

const toBase64 = (buffer: any) =>
  // @ts-ignore
  btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)));

const fromBase64 = (b64Text: string) => Buffer.from(b64Text, "base64");
