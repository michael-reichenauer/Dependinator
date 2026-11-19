import { configure } from "./lib";

// Runs when the file is imported
configure(Number(process.env.VAT ?? "0.25"));
