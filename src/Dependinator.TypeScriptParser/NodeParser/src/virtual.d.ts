// The compiler's library declarations by file name, bundled in by build.mjs
declare module "virtual:typescript-libraries" {
    const libraries: Record<string, string>;
    export default libraries;
}
