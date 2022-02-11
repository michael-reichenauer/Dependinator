import { ICrypt, ICryptKey } from "./crypt";
import { di, diKey, singleton } from "./di";
import { User } from "./Api";

export const IDataCryptKey = diKey<IDataCrypt>();
export interface IDataCrypt {
  // Create a unique data encryption key (DEK), which is wrapped/encrypted by a
  // key encryption key (KEK), which was derived from the username and password
  // The returned string is safe to store, since it is encrypted by the KEK (derived by user)
  generateWrappedDataEncryptionKey(user: User): Promise<string>;

  // Expands a password by using a derive bits hash like e.g. PBKDF2, which makes it much harder to
  // use brute force to hack the password. This is used both on the client side as well as the server
  // side to reduce work load on server side and to ensure original password never leaves the client.
  expandPassword(user: User): Promise<string>;

  // Unwraps/decrypts the wrapped data encryption key (DEK) using the
  // key encryption key (KEK), which was derived from the username and password
  // The returned DEK key is a secret that should be handled with great care and not stored.
  unwrapDataEncryptionKey(wrappedDek: string, user: User): Promise<CryptoKey>;

  // Encrypt a text block using the data encryption key DEK
  encryptText(text: string, dek: CryptoKey): Promise<string>;

  // Decrypts a text block using the data encryption key (DEK)
  decryptText(encryptedText: string, dek: CryptoKey): Promise<string>;
}

type WrappedDek = { key: any; iv: any };
type EncryptedPacket = { data: string; iv: string };

@singleton(IDataCryptKey)
export class DataCrypt {
  constructor(private crypt: ICrypt = di(ICryptKey)) {}

  public async expandPassword(user: User): Promise<string> {
    const salt = await this.crypt.sha256(user.username);
    const bits = await this.crypt.deriveBits(user.password, salt);
    return toBase64(bits);
  }

  public async generateWrappedDataEncryptionKey(user: User): Promise<string> {
    // Derive a key encryption key (KEK) to wrap/encrypt the DEK key
    const kek = await this.deriveKeyEncryptionKey(user);

    // The new unique data encryption key (DEK) to encrypt/decrypt data
    const dek = await this.crypt.generateKey(["encrypt", "decrypt"]);

    // encrypt/wrap the DEK using the KEK
    const wrappedDek = await this.wrapDataEncryptionKey(dek, kek);

    const wrappedDekJson = JSON.stringify(wrappedDek);

    return utf8_to_b64(wrappedDekJson);
  }

  public async unwrapDataEncryptionKey(
    wrappedDek: string,
    user: User
  ): Promise<CryptoKey> {
    // Derive a key encryption key (KEK) to unwrap/decrypt the DEK key
    const kek = await this.deriveKeyEncryptionKey(user);

    // Extract the encrypted text and encrypted DEK key
    const wrappedDekJson = b64_to_utf8(wrappedDek);
    const wDek = JSON.parse(wrappedDekJson);

    // Decrypt/unwrap the DEK key using the KEK key
    const dek = await this.unWrapDataEncryptionKey(wDek, kek);
    return dek;
  }

  public async encryptText(text: string, dek: CryptoKey): Promise<string> {
    // Encrypt the text
    const textBytes = new TextEncoder().encode(text);
    const cipher = await this.crypt.encryptData(textBytes, dek);
    const encryptedData = cipher.data;

    // The unique random initialization vector
    const iv = cipher.iv;

    // Pack the encrypted text and encrypted DEK key into a encrypted text string
    const encryptedPacket: EncryptedPacket = {
      data: toBase64(encryptedData),
      iv: toBase64(iv),
    };
    const encryptedJson = JSON.stringify(encryptedPacket);
    const encryptedText = utf8_to_b64(encryptedJson);

    return encryptedText;
  }

  public async decryptText(
    encryptedText: string,
    dek: CryptoKey
  ): Promise<string> {
    // Extract the encrypted text and encrypted DEK key
    const encryptedJson = b64_to_utf8(encryptedText);
    const encryptedPacket: EncryptedPacket = JSON.parse(encryptedJson);

    // The unique random initialization vector
    const iv = fromBase64(encryptedPacket.iv);

    // Decrypt the encrypted text using the DEK key
    const encryptedData = fromBase64(encryptedPacket.data);
    const decryptedData = await this.crypt.decryptData(encryptedData, iv, dek);

    const decryptedText = new TextDecoder().decode(decryptedData);
    return decryptedText;
  }

  // Creates a key encryption key (KEK) to encrypt each unique data encryption key (DEK), which is
  // used to encrypt each encrypted text block
  private async deriveKeyEncryptionKey(user: User): Promise<CryptoKey> {
    // using hash of username as salt. Usually a salt is a random number, but in this case, it
    // is sufficient and convenient to use the username as salt
    const salt = await this.crypt.sha256(user.username);

    // Derive KEK key to wrap/encrypt and unwrap/decrypt DEK key
    return await this.crypt.deriveKey(user.password, salt, [
      "wrapKey",
      "unwrapKey",
    ]);
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
