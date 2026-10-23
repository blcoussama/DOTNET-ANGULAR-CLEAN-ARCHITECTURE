// Serveur des tests navigateur : sert le build de production Angular (comme nginx en production) et
// relaie /api vers l'API. Évite le serveur de développement (rechargement à chaud, scripts injectés
// par des extensions d'éditeur) pour tester exactement ce qui sera déployé.
//   node serve-spa.js <dossier du build> <port> <URL de l'API>
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const [root, port, apiUrl] = [path.resolve(process.argv[2]), Number(process.argv[3]), new URL(process.argv[4])];
const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon',
  '.woff2': 'font/woff2', '.woff': 'font/woff', '.txt': 'text/plain; charset=utf-8',
};

function proxy(req, res) {
  const upstream = http.request(
    { host: apiUrl.hostname, port: apiUrl.port, method: req.method, path: req.url, headers: req.headers },
    (answer) => {
      res.writeHead(answer.statusCode, answer.headers);
      answer.pipe(res);
    },
  );
  upstream.on('error', () => {
    res.writeHead(502, { 'content-type': 'text/plain' });
    res.end('API indisponible');
  });
  req.pipe(upstream);
}

http
  .createServer((req, res) => {
    if (req.url.startsWith('/api/')) return proxy(req, res);
    const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    let file = path.join(root, pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
      file = path.join(root, 'index.html'); // routes de la SPA
    }
    res.writeHead(200, { 'content-type': TYPES[path.extname(file)] || 'application/octet-stream' });
    fs.createReadStream(file).pipe(res);
  })
  .listen(port, '127.0.0.1', () => console.log(`SPA ${root} sur http://localhost:${port}, /api -> ${apiUrl.origin}`));
