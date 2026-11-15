export const settings = { vat: 0.25, name: "shop" };

export function configure(vat: number): void {
    settings.vat = vat;
}
