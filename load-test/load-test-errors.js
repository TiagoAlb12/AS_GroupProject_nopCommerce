import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
    vus: 5,
    duration: '20s',
};

export default function() {
    // 1. Produto que não existe (404)
    let invalidId = Math.floor(Math.random() * 9999) + 10000;
    let res = http.get(`http://localhost:80/product/${invalidId}`);
    check(res, { 
        'product not found': (r) => r.status === 404 
    });
    
    sleep(0.3);
    
    // 2. URL inválida (404)
    let invalidUrls = ['/invalid-page', '/api/error', '/product/abc'];
    let url = invalidUrls[Math.floor(Math.random() * invalidUrls.length)];
    res = http.get(`http://localhost:80${url}`);
    check(res, { 
        'invalid url': (r) => r.status === 404 
    });
    
    sleep(0.3);
    
    // 3. Pesquisa inválida (pode dar erro de validação)
    let invalidTerms = ['!!!', ' ', 'a', ''];
    let term = invalidTerms[Math.floor(Math.random() * invalidTerms.length)];
    res = http.get(`http://localhost:80/search?q=${term}`);
    check(res, { 
        'search with invalid term': (r) => r.status !== 200 
    });
    
    sleep(0.5);
}