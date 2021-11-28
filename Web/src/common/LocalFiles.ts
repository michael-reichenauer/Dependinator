import FileSaver from "file-saver";

export interface ILocalFiles {
  saveFile(fileName: string, fileText: string): void;
  loadFile(): Promise<string>;
}

export default class LocalFiles implements ILocalFiles {
  saveFile(fileName: string, fileText: string): void {
    const blob = new Blob([fileText], { type: "text/plain;charset=utf-8" });
    FileSaver.saveAs(blob, fileName);
  }

  loadFile(): Promise<string> {
    return new Promise((resolve, reject) => {
      const readFile = this.buildFileSelector((e: any) => {
        var file = e.path[0].files[0];
        if (!file) {
          console.log("No file");
          reject(new Error("No file"));
        }

        const reader = new FileReader();

        reader.onload = (e: any) => resolve(e.target.result);
        reader.onerror = (_) => reject(reader.error);

        reader.readAsText(file);
      });

      // Trigger browser to show 'open file' dialog to read file
      readFile.click();
    });
  }

  buildFileSelector(selectedHandler: any) {
    const fileSelector = document.createElement("input");
    fileSelector.setAttribute("type", "file");
    fileSelector.setAttribute("multiple", "multiple");
    fileSelector.addEventListener("change", selectedHandler, false);

    return fileSelector;
  }
}
