import http from 'k6/http';
import { check, sleep, fail } from 'k6';

// Performance test for the read-heavy customer-facing endpoints: account listing,
// transaction filtering (the paginated/indexed hot path — see
// TransactionConfiguration.cs composite indexes and ADR-0007), and the transaction
// summary aggregation. See ../README.md for the assumptions and target thresholds
// this script's `options.thresholds` encode, and for how to run it.
//
// Run with, e.g.:
//   k6 run -e BASE_URL=http://localhost:5001 -e KEYCLOAK_URL=http://localhost:8081 perf/k6/transactions-read.js

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5001';
const KEYCLOAK_URL = __ENV.KEYCLOAK_URL || 'http://localhost:8081';
const KEYCLOAK_REALM = __ENV.KEYCLOAK_REALM || 'transaction-aggregation';
const TRANSACTION_COUNT = Number(__ENV.SEED_TRANSACTION_COUNT || 200);

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
    // Targets, not aspirations pulled from nowhere — see perf/README.md
    // "Assumptions and targets" for the reasoning behind each number.
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:filter_transactions}': ['p(95)<300'],
    'http_req_duration{endpoint:get_accounts}': ['p(95)<200'],
    'http_req_duration{endpoint:transaction_summary}': ['p(95)<400'],
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

function seedCustomer() {
  const email = `perf-${Date.now()}-${Math.random().toString(36).slice(2)}@example.com`;
  const password = 'Password1!';
  const createRes = http.post(
    `${BASE_URL}/api/v1/customers`,
    JSON.stringify({ Email: email, Name: 'Perf Test User', Password: password }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (createRes.status !== 201) {
    fail(`Customer creation failed: ${createRes.status} ${createRes.body}`);
  }
  const customerId = createRes.json();
  return { customerId, email, password };
}

function seedAccountAndTransactions(token, customerId) {
  const authHeaders = { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } };

  const accountRes = http.post(
    `${BASE_URL}/api/v1/customers/${customerId}/accounts`,
    JSON.stringify({ AccountNumber: `perf-${customerId}`, AccountName: 'Perf Account', AccountType: 0, Currency: 'ZAR' }),
    authHeaders,
  );
  if (accountRes.status !== 201) {
    fail(`Account creation failed: ${accountRes.status} ${accountRes.body}`);
  }

  // Seeding transactions goes through the same webhook ingestion path production
  // traffic uses (realistic workload, not a direct DB insert) — requires an active
  // webhook source. If none is seeded in this environment, skip transaction seeding
  // and note it; the read endpoints are still exercised against an empty/small dataset.
  // For a representative load test, seed TRANSACTION_COUNT transactions per customer
  // via the webhook endpoint against a pre-linked bank account before running this script.
}

export function setup() {
  const { customerId, email, password } = seedCustomer();
  const token = getAccessToken(email, password);
  seedAccountAndTransactions(token, customerId);
  return { customerId, token };
}

export default function (data) {
  const { customerId, token } = data;
  const authHeaders = { headers: { Authorization: `Bearer ${token}` } };

  const accountsRes = http.get(`${BASE_URL}/api/v1/customers/${customerId}/accounts`, {
    ...authHeaders,
    tags: { endpoint: 'get_accounts' },
  });
  check(accountsRes, { 'get accounts: status 200': (r) => r.status === 200 });

  const filterRes = http.get(
    `${BASE_URL}/api/v1/customers/${customerId}/transactions/filter?pageNumber=1&pageSize=25`,
    { ...authHeaders, tags: { endpoint: 'filter_transactions' } },
  );
  check(filterRes, { 'filter transactions: status 200': (r) => r.status === 200 });

  const summaryRes = http.get(
    `${BASE_URL}/api/v1/customers/${customerId}/transactions/summary` +
      `?startDate=2020-01-01&endDate=2030-01-01`,
    { ...authHeaders, tags: { endpoint: 'transaction_summary' } },
  );
  check(summaryRes, { 'transaction summary: status 200': (r) => r.status === 200 });

  sleep(1);
}
