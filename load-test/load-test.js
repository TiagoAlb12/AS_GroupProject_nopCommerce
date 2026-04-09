import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
    vus: 10,
    duration: '30s',
};

export default function() {
    // 1. Homepage
    let res = http.get('http://localhost:80/');
    check(res, { 'homepage status 200': (r) => r.status === 200 });
    sleep(0.5);
    
    // 2. Categorias
    let categories = ['/computers', '/electronics', '/apparel', '/books'];
    let category = categories[Math.floor(Math.random() * categories.length)];
    res = http.get(`http://localhost:80${category}`);
    check(res, { 'category status 200': (r) => r.status === 200 });
    sleep(0.5);
    
    // 3. Pesquisas
    let terms = ['phone', 'laptop', 'camera', 'iphone', 'computer'];
    let term = terms[Math.floor(Math.random() * terms.length)];
    res = http.get(`http://localhost:80/search?q=${term}`);
    check(res, { 'search status 200': (r) => r.status === 200 });
    sleep(0.5);
    
    // 4. Produtos (com URLs corretos)
    let productUrls = [
        '/build-your-own-computer',
        '/apple-macbook-pro', 
        '/htc-smartphone',
        '/25-virtual-gift-card'
    ];
    let productUrl = productUrls[Math.floor(Math.random() * productUrls.length)];
    res = http.get(`http://localhost:80${productUrl}`);
    check(res, { 'product status 200': (r) => r.status === 200 });
    
    sleep(1);
}