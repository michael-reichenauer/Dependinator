// Inspired of https://levelup.gitconnected.com/pretty-print-your-site-with-javascript-d69f63956529
export default class Printer {
  static registerPrintKey(action: () => void) {
    // override Ctrl/Cmd + P

    // @ts-ignore
    const handler = (event: any) => {
      if ((event.ctrlKey || event.metaKey) && event.key === "p") {
        action();
        event.preventDefault();
        return false;
      }
    };

    document.addEventListener("keydown", handler, false);
    return handler;
  }

  static deregisterPrintKey(handler: any) {
    document.removeEventListener("keydown", handler, false);
  }

  print(pages: string[]) {
    // Create one page with page brakes between
    const pagesHtml = pages.join(
      '<p style="page-break-after: always;">&nbsp;</p>'
    );

    // create a hidden iframe named PrettyPrintFrame
    const prettyPrintIframe = document.createElement("iframe");
    prettyPrintIframe.setAttribute("id", "PrettyPrintFrame");
    prettyPrintIframe.setAttribute("name", "PrettyPrintFrame");
    prettyPrintIframe.setAttribute("style", "display: none;");

    // add newly created iframe to the current DOM
    document.body.appendChild(prettyPrintIframe);

    // add generated header content
    // @ts-ignore
    prettyPrintIframe.contentWindow.document.head.innerHTML =
      this.generateHeaderHtml();
    // @ts-ignore
    prettyPrintIframe.contentWindow.document.body.innerHTML = pagesHtml;

    try {
      // reference to iframe window
      const contentWindow = prettyPrintIframe.contentWindow;

      // execute iframe print command
      // @ts-ignore
      const result = contentWindow.document.execCommand("print", false, null);

      // iframe print listener
      // @ts-ignore
      const printListener = contentWindow.matchMedia("print");
      printListener.addListener(function (pl) {
        if (!pl.matches) {
          // remove the hidden iframe from the DOM
          // prettyPrintIframe.remove();
        }
      });

      // if execCommand is unsupported
      if (!result) {
        contentWindow?.print();
      }
    } catch (e) {
      // print fallback
      // @ts-ignore
      window.frames["PrettyPrintFrame"].focus();
      // @ts-ignore
      window.frames["PrettyPrintFrame"].print();
    }
  }

  generateHeaderHtml() {
    let headerHtml = "";

    // loop through the styleSheets object and pull in all styles
    for (let i = 0; i < document.styleSheets.length; i++) {
      headerHtml += "<style>";

      try {
        for (let j = 0; j < document.styleSheets[i].cssRules.length; j++) {
          headerHtml += document.styleSheets[i].cssRules[j].cssText || "";
        }
      } catch (e) {}

      headerHtml += "</style>";
    }

    headerHtml += this.generateGlobalCss();

    return headerHtml;
  }

  generateGlobalCss() {
    // add any global css you want to apply to all pretty print pages
    let css = "<style>";

    // global css
    css += "body {padding: 0px 0px;  }";
    css += "svg { page-break-inside: avoid; }";
    css += "table tr { page-break-inside: avoid; }";
    css += "table td { vertical-align: top; padding: 0px 0px;}";
    css += "img { height: 100px !important; width: 100px !important; }";
    css += "@page { margin: 0cm; }";

    css += "</style>";
    return css;
  }
}
