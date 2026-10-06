import http from 'k6/http';
import { check, sleep, fail } from 'k6';

// Read-heavy staff endpoints. Thresholds and assumptions: see ../README.md.
// Run: k6 run -e STAFF_PASSWORD=<from .env> [-e BASE_URL=... -e KEYCLOAK_URL=...] perf/k6/transactions-read.js

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5001';
const KEYCLOAK_URL = __ENV.KEYCLOAK_URL || 'http://localhost:8081';
const KEYCLOAK_REALM = __ENV.KEYCLOAK_REALM || 'transaction-aggregation';
const STAFF_EMAIL = __ENV.STAFF_EMAIL || 'staff@test.com';
const STAFF_PASSWORD = __ENV.STAFF_PASSWORD;
if (!STAFF_PASSWORD) {
  throw new Error('Set STAFF_PASSWORD (-e STAFF_PASSWORD=...) to the dev staff user password.');
}

export const options = {
  scenarios: {
    read_workload: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '1m', target: 20 },
        { duration: '30s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:list_transactions}': ['p(95)<300'],
    'http_req_duration{endpoint:transaction_summary}': ['p(95)<400'],
    'http_req_duration{endpoint:institution_breakdown}': ['p(95)<400'],
    'http_req_duration{endpoint:category_breakdown}': ['p(95)<400'],
  },
};

function getAccessToken(email, password) {
  const tokenUrl = `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`;
  const res = http.post(
    tokenUrl,
    {
      grant_type: 'password',
      client_id: 'transaction-perf-test',
      username: email,
      password: password,
    },
    { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } },
  );
  if (res.status !== 200) {
    fail(`Keycloak token request failed: ${res.status} ${res.body}`);
  }
  return res.json('access_token');
}

export function setup() {
  return { token: getAccessToken(STAFF_EMAIL, STAFF_PASSWORD) };
}

export default function (data) {
  const authHeaders = { headers: { Authorization: `Bearer ${data.token}` } };

  const listRes = http.get(`${BASE_URL}/api/v1/transactions?pageSize=25`, {
    ...authHeaders,
    tags: { endpoint: 'list_transactions' },
  });
  check(listRes, { 'list transactions: status 200': (r) => r.status === 200 });

  const summaryRes = http.get(
    `${BASE_URL}/api/v1/transactions/summary?startDate=2020-01-01&endDate=2030-01-01`,
    { ...authHeaders, tags: { endpoint: 'transaction_summary' } },
  );
  check(summaryRes, { 'transaction summary: status 200': (r) => r.status === 200 });

  const institutionsRes = http.get(`${BASE_URL}/api/v1/transactions/aggregates/institutions`, {
    ...authHeaders,
    tags: { endpoint: 'institution_breakdown' },
  });
  check(institutionsRes, { 'institution breakdown: status 200': (r) => r.status === 200 });

  const categoriesRes = http.get(`${BASE_URL}/api/v1/transactions/aggregates/categories`, {
    ...authHeaders,
    tags: { endpoint: 'category_breakdown' },
  });
  check(categoriesRes, { 'category breakdown: status 200': (r) => r.status === 200 });

  sleep(1);
}
