import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
    stages: [
        { duration: '20s', target: 5 },    // Aquecimento (20s, 5 VUs)
        { duration: '40s', target: 15 },   // Pico (40s, 15 VUs)
        { duration: '20s', target: 5 },    // Descida (20s, 5 VUs)
        { duration: '10s', target: 0 },    // Fim (10s, 0 VUs)
    ],
    thresholds: {
        http_req_duration: ['p(95)<800'],  // 95% dos pedidos < 800ms
        'http_req_duration{type:search}': ['p(95)<500'], // Pesquisas rápidas
    },
};

export default function() {
    // 80% de chance de fazer pesquisa (comportamento normal)
    if (Math.random() < 0.8) {
        let terms = ['iphone', 'camera', 'laptop', 'speaker', 'tablet', 'headphones'];
        let term = terms[Math.floor(Math.random() * terms.length)];
        
        let res = http.get(`http://localhost:80/search?q=${term}`, {
            tags: { type: 'search' }
        });
        check(res, { 'search status 200': (r) => r.status === 200 });
        
        sleep(0.5);
    }
    
    // 60% de chance de ver produto (depois de pesquisar)
    if (Math.random() < 0.6) {
        let productUrls = [
            '/apple-macbook-pro',
            '/htc-smartphone',
            '/portable-sound-speakers',
            '/beats-pill-wireless-speaker',
            '/nokia-lumia-1020',
            '/build-your-own-computer'
        ];
        let productUrl = productUrls[Math.floor(Math.random() * productUrls.length)];
        
        let res = http.get(`http://localhost:80${productUrl}`, {
            tags: { type: 'product' }
        });
        check(res, { 'product status 200': (r) => r.status === 200 });
    }
    
    sleep(1);
}