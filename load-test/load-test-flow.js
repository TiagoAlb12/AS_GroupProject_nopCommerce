/*
 *   0:00-2:30 normal traffic and valid OrderCreated events
 *   2:30-3:30 OIS stopped by the orchestrator; queue accumulates
 *   3:30-4:30 OIS restarted; queue drains
 *   4:30-5:00 invalid messages; DLQ increases
 *
 * Run:
 *   k6 run load-test/load-test-flow.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import encoding from 'k6/encoding';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:80';
const RABBITMQ_API = __ENV.RABBITMQ_API || 'http://localhost:15672/api';
const RABBITMQ_USER = __ENV.RABBITMQ_USER || 'nopcommerce';
const RABBITMQ_PASS = __ENV.RABBITMQ_PASS || 'nopCommerce_rabbit_password';
const RABBITMQ_VHOST = __ENV.RABBITMQ_VHOST || '%2F';
const ORDER_EXCHANGE = __ENV.ORDER_EXCHANGE || 'nopcommerce.order.events';
const ORDER_ROUTING_KEY = __ENV.ORDER_ROUTING_KEY || 'order.created';

const productSlugs = [
  '/apple-iphone-16-128gb',
  '/htc-smartphone',
  '/nokia-lumia-1020',
  '/asus-laptop',
  '/leica-t-mirrorless-digital-camera',
  '/nikon-d5500-dslr-red',
  '/build-your-own-computer',
];

const browseSlugs = [
  '/',
  '/electronics',
  '/computers',
  '/apparel',
  '/books',
];

const searchTerms = ['iphone', 'camera', 'laptop', 'nokia', 'asus'];

export const options = {
  scenarios: {
    user_traffic: {
      executor: 'ramping-vus',
      exec: 'userTraffic',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '60s', target: 12 },
        { duration: '60s', target: 10 },
        { duration: '60s', target: 10 },
        { duration: '60s', target: 5 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
    valid_order_events: {
      executor: 'per-vu-iterations',
      exec: 'validOrderEvents',
      vus: 1,
      iterations: 1,
      startTime: '0s',
      maxDuration: '275s',
    },
    invalid_order_events: {
      executor: 'per-vu-iterations',
      exec: 'invalidOrderEvents',
      vus: 1,
      iterations: 1,
      startTime: '270s',
      maxDuration: '35s',
    },
  },
  thresholds: {
    'http_req_duration{flow:browse}': ['p(95)<3000'],
    'http_req_duration{flow:product}': ['p(95)<3000'],
    'http_req_duration{flow:rabbitmq_publish_valid}': ['p(95)<1000'],
    'http_req_duration{flow:rabbitmq_publish_invalid}': ['p(95)<1000'],
  },
};

export function userTraffic() {
  const browsePath = pick(browseSlugs);
  const browse = http.get(`${BASE_URL}${browsePath}`, { tags: { flow: 'browse' } });
  check(browse, { 'browse returned 2xx/3xx': (r) => r.status >= 200 && r.status < 400 });

  if (Math.random() < 0.7) {
    const term = pick(searchTerms);
    const search = http.get(`${BASE_URL}/search?q=${encodeURIComponent(term)}`, { tags: { flow: 'search' } });
    check(search, { 'search returned 2xx/3xx': (r) => r.status >= 200 && r.status < 400 });
  }

  if (Math.random() < 0.8) {
    const product = http.get(`${BASE_URL}${pick(productSlugs)}`, { tags: { flow: 'product' } });
    check(product, { 'product returned 2xx/3xx': (r) => r.status >= 200 && r.status < 400 });
  }

  sleep(1);
}

export function validOrderEvents() {
  const startedAt = Date.now();
  let sequence = 0;
  let announcedQueueBuildUp = false;
  let announcedQueueDrain = false;

  console.log('[phase 1] 0:00-2:30 normal system, valid events are consumed');

  while (elapsedSeconds(startedAt) < 270) {
    const elapsed = elapsedSeconds(startedAt);

    if (elapsed >= 150 && elapsed < 210 && !announcedQueueBuildUp) {
      console.log('[phase 2] 2:30-3:30 OIS should be stopped by the PowerShell orchestrator');
      announcedQueueBuildUp = true;
    }

    if (elapsed >= 210 && elapsed < 270 && !announcedQueueDrain) {
      console.log('[phase 3] 3:30-4:30 OIS should be running again and draining the queue');
      announcedQueueDrain = true;
    }

    sequence += 1;
    publishOrderCreated(validOrderPayload(sequence), 'rabbitmq_publish_valid');

    if (elapsed < 150) {
      sleep(3);
    } else if (elapsed < 210) {
      sleep(1);
    } else {
      sleep(2);
    }
  }
}

export function invalidOrderEvents() {
  console.log('[phase 4] 4:30-5:00 invalid messages, DLQ should increase');

  const invalidPayloads = [
    '{ this is not json',
    '{"eventId":"not-a-guid","eventType":"OrderCreated","orderId":910001,"customerId":1,"createdAt":"2026-01-01T00:00:00Z","total":10.50}',
    '{"eventId":"00000000-0000-0000-0000-000000000000","eventType":"OrderCreated","orderId":"NOT_A_NUMBER","customerId":1,"createdAt":"2026-01-01T00:00:00Z","total":10.50}',
    '{"eventId":"00000000-0000-0000-0000-000000000000","eventType":"OrderCreated","orderId":910003,"customerId":"BAD","createdAt":"2026-01-01T00:00:00Z","total":10.50}',
  ];

  for (let i = 0; i < 30; i += 1) {
    publishOrderCreated(invalidPayloads[i % invalidPayloads.length], 'rabbitmq_publish_invalid');
    sleep(1);
  }
}

export default function () {
  userTraffic();
}

function publishOrderCreated(payload, flowTag) {
  const body = JSON.stringify({
    properties: {
      content_type: 'application/json',
      delivery_mode: 2,
      message_id: randomId(),
      type: 'OrderCreated',
      headers: { 'x-load-test': 'checkout-flow' },
    },
    routing_key: ORDER_ROUTING_KEY,
    payload,
    payload_encoding: 'string',
  });

  const res = http.post(
    `${RABBITMQ_API}/exchanges/${RABBITMQ_VHOST}/${ORDER_EXCHANGE}/publish`,
    body,
    { headers: rabbitHeaders(), tags: { flow: flowTag } },
  );

  check(res, {
    'rabbit publish accepted': (r) => r.status === 200,
    'rabbit message routed': (r) => {
      if (r.status !== 200) return false;
      try {
        return JSON.parse(r.body).routed === true;
      } catch (_) {
        return false;
      }
    },
  });

  if (res.status !== 200) {
    console.warn(`RabbitMQ publish failed: status=${res.status} body=${res.body}`);
  }
}

function validOrderPayload(sequence) {
  return JSON.stringify({
    eventId: randomId(),
    eventType: 'OrderCreated',
    orderId: 900000 + sequence,
    customerId: 1 + (sequence % 20),
    createdAt: new Date().toISOString(),
    total: Number((25 + Math.random() * 250).toFixed(2)),
  });
}

function rabbitHeaders() {
  const token = encoding.b64encode(`${RABBITMQ_USER}:${RABBITMQ_PASS}`);
  return {
    'Content-Type': 'application/json',
    Authorization: `Basic ${token}`,
  };
}

function pick(items) {
  return items[Math.floor(Math.random() * items.length)];
}

function elapsedSeconds(startedAt) {
  return Math.floor((Date.now() - startedAt) / 1000);
}

function randomId() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
