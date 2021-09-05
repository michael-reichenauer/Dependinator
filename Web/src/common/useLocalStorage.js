import { useState } from 'react';

export const useLocalStorage = (key, initialValue) => {
    // State to store our value
    // Pass initial state function to useState so logic is only executed once
    const [storedValue, setStoredValue] = useState(() => {
        try {
            // Get from local storage by key
            const item = window.localStorage.getItem(key);
            // Parse stored json or if none return initialValue
            if (item) {
                return JSON.parse(item)
            }

            // Item not i local storage, lets store the initial value (function or value)
            const value = initialValue instanceof Function ? initialValue() : initialValue;
            window.localStorage.setItem(key, JSON.stringify(value));
            return value;
        } catch (error) {
            // If error also return initialValue
            console.log(error);
            const value = initialValue instanceof Function ? initialValue() : initialValue;
            return value;
        }
    });

    // Return a wrapped version of useState's setter function that ...
    // ... persists the new value to localStorage.
    const setValue = value => {
        try {
            // Allow value to be a function so we have same API as useState
            const valueToStore =
                value instanceof Function ? value(storedValue) : value;
            // Save state
            setStoredValue(valueToStore);
            // Save to local storage
            window.localStorage.setItem(key, JSON.stringify(valueToStore));
        } catch (error) {
            // A more advanced implementation would handle the error case
            console.log(error);
        }
    };

    return [storedValue, setValue];
}