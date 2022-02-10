// import timing from './timing';

import { diKey, singleton } from "./di";

const saltLength = 16;
const keyLength = 256;
const ivLength = 12;
const algorithm = "AES-GCM";
const deriveAlgorithm = "PBKDF2";
const deriveIterations = 250000;

export const ICryptKey = diKey<ICrypt>();
export interface ICrypt {
  generateSalt(): Uint8Array;
  sha256(text: string): Promise<ArrayBuffer>;
  deriveKey(
    password: string,
    salt: ArrayBuffer,
    keyUsage: KeyUsage[]
  ): Promise<CryptoKey>;
  encryptData(
    data: ArrayBuffer,
    key: CryptoKey
  ): Promise<{ data: any; iv: Uint8Array }>;
  decryptData(cipher: any, iv: Buffer, key: CryptoKey): Promise<any>;
  generateKey(keyUsage: KeyUsage[]): Promise<CryptoKey>;
  generateIv(): Uint8Array;
  wrapKey(
    key: CryptoKey,
    iv: Uint8Array,
    wrappingKey: CryptoKey
  ): Promise<ArrayBuffer>;
  unWrapKey(
    wrapped: any,
    iv: Buffer,
    wrappingKey: CryptoKey
  ): Promise<CryptoKey>;
}

@singleton(ICryptKey)
export class Crypt {
  public generateSalt(): Uint8Array {
    return crypto.getRandomValues(new Uint8Array(saltLength));
  }

  async sha256(text: string): Promise<ArrayBuffer> {
    const textBytes = new TextEncoder().encode(text);
    const sha256Hash = await crypto.subtle.digest("SHA-256", textBytes);
    return sha256Hash;
  }

  public async deriveKey(
    password: string,
    salt: ArrayBuffer,
    keyUsage: KeyUsage[]
  ): Promise<CryptoKey> {
    const passwordBytes = new TextEncoder().encode(password);
    const passwordKey = await crypto.subtle.importKey(
      "raw",
      passwordBytes,
      deriveAlgorithm,
      false,
      ["deriveKey"]
    );

    return await crypto.subtle.deriveKey(
      {
        name: deriveAlgorithm,
        salt: salt,
        iterations: deriveIterations,
        hash: "SHA-256",
      },
      passwordKey,
      { name: algorithm, length: keyLength },
      false,
      keyUsage
    );
  }

  public async generateKey(keyUsage: KeyUsage[]): Promise<CryptoKey> {
    return await crypto.subtle.generateKey(
      {
        name: algorithm,
        length: keyLength,
      },
      true, // Key is extractable so it kan be wrapped
      keyUsage
    );
  }

  public async encryptData(
    data: ArrayBuffer,
    key: CryptoKey
  ): Promise<{ data: any; iv: Uint8Array }> {
    const iv = crypto.getRandomValues(new Uint8Array(ivLength));
    const encryptedData = await crypto.subtle.encrypt(
      { name: algorithm, iv: iv },
      key,
      data
    );
    return { data: encryptedData, iv: iv };
  }

  public async decryptData(
    cipher: any,
    iv: Buffer,
    key: CryptoKey
  ): Promise<any> {
    return await crypto.subtle.decrypt(
      { name: algorithm, iv: iv },
      key,
      cipher
    );
  }

  public generateIv(): Uint8Array {
    return crypto.getRandomValues(new Uint8Array(ivLength));
  }

  public async wrapKey(
    key: CryptoKey,
    iv: Uint8Array,
    wrappingKey: CryptoKey
  ): Promise<ArrayBuffer> {
    return await window.crypto.subtle.wrapKey("raw", key, wrappingKey, {
      name: algorithm,
      iv: iv,
      tagLength: 128,
    });
  }

  public async unWrapKey(
    wrapped: any,
    iv: Buffer,
    wrappingKey: CryptoKey
  ): Promise<CryptoKey> {
    return await window.crypto.subtle.unwrapKey(
      "raw",
      wrapped,
      wrappingKey,
      {
        name: algorithm,
        iv: iv,
        tagLength: 128,
      },
      {
        // wrapped key
        name: algorithm,
        length: keyLength,
      },
      false, // unwrapped key should not be extractable
      ["encrypt", "decrypt"] // Key usages
    );
  }
}
