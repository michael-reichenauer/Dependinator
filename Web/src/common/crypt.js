// import timing from './timing';

const saltLength = 16
const keyLength = 256
const ivLength = 12
const algorithm = "AES-GCM"
const deriveAlgorithm = "PBKDF2"
const deriveIterations = 250000

class Crypt {
    generateSalt() {
        return crypto.getRandomValues(new Uint8Array(saltLength))
    }

    generateIv() {
        return crypto.getRandomValues(new Uint8Array(ivLength))
    }

    async deriveKey(passwordText, salt, keyUsage) {
        const password = new TextEncoder().encode(passwordText)
        const passwordKey = await crypto.subtle.importKey(
            "raw", password, deriveAlgorithm, false, ["deriveKey"]);

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
            keyUsage,
        );
    }

    async generateKey() {
        return await crypto.subtle.generateKey(
            {
                name: algorithm,
                length: keyLength,
            }, true, // Key is extractable so it kan be wrapped
            ['encrypt', 'decrypt'] // usage for key
        )
    }



    async encryptData(data, key) {
        const iv = crypto.getRandomValues(new Uint8Array(ivLength));
        const encryptedData = await crypto.subtle.encrypt({ name: algorithm, iv: iv, }, key, data);
        return { data: encryptedData, iv: iv }
    }

    async decryptData(cipher, key, iv) {
        return await crypto.subtle.decrypt({ name: algorithm, iv: iv }, key, cipher);
    }


    async wrapKey(key, wrappingKey, iv) {
        return await window.crypto.subtle.wrapKey(
            "raw",
            key,
            wrappingKey,
            {
                name: algorithm,
                iv: iv,
                tagLength: 128,
            }
        )
    }

    async unWrapKey(wrapped, wrappingKey, iv) {
        return await window.crypto.subtle.unwrapKey(
            "raw",
            wrapped,
            wrappingKey,
            {
                name: algorithm,
                iv: iv,
                tagLength: 128,
            },
            {   // wrapped key
                name: algorithm,
                length: keyLength
            },
            false, // unwrapped key should not be extractable 
            ["encrypt", "decrypt"] // Key usages 
        )
    }
}

export const crypt = new Crypt()
