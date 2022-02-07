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
  sha256(message: string): Promise<ArrayBuffer>;
  deriveKey(passwordText: string, salt: any, keyUsage: any): Promise<CryptoKey>;
  encryptData(
    data: any,
    key: CryptoKey
  ): Promise<{ data: any; iv: Uint8Array }>;
  decryptData(cipher: any, key: CryptoKey, iv: Buffer): Promise<any>;
  generateKey(): Promise<CryptoKey>;
  generateIv(): Uint8Array;
  wrapKey(
    key: CryptoKey,
    wrappingKey: CryptoKey,
    iv: Uint8Array
  ): Promise<ArrayBuffer>;
  unWrapKey(
    wrapped: any,
    wrappingKey: CryptoKey,
    iv: Buffer
  ): Promise<CryptoKey>;
}

@singleton(ICryptKey)
export class Crypt {
  public generateSalt(): Uint8Array {
    return crypto.getRandomValues(new Uint8Array(saltLength));
  }

  async sha256(message: string): Promise<ArrayBuffer> {
    const encoder = new TextEncoder();
    const data = encoder.encode(message);
    const hash = await crypto.subtle.digest("SHA-256", data);
    return hash;
  }

  public async deriveKey(
    passwordText: string,
    salt: any,
    keyUsage: any
  ): Promise<CryptoKey> {
    const password = new TextEncoder().encode(passwordText);
    const passwordKey = await crypto.subtle.importKey(
      "raw",
      password,
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

  public async generateKey(): Promise<CryptoKey> {
    return await crypto.subtle.generateKey(
      {
        name: algorithm,
        length: keyLength,
      },
      true, // Key is extractable so it kan be wrapped
      ["encrypt", "decrypt"] // usage for key
    );
  }

  public async encryptData(
    data: any,
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
    key: CryptoKey,
    iv: Buffer
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
    wrappingKey: CryptoKey,
    iv: Uint8Array
  ): Promise<ArrayBuffer> {
    return await window.crypto.subtle.wrapKey("raw", key, wrappingKey, {
      name: algorithm,
      iv: iv,
      tagLength: 128,
    });
  }

  public async unWrapKey(
    wrapped: any,
    wrappingKey: CryptoKey,
    iv: Buffer
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
