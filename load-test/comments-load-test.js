import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// ── Config ────────────────────────────────────────────────────────────────────
const API_URL = 'https://pcd-realworld-api.azurewebsites.net';

// Custom metrics
const errorRate = new Rate('error_rate');
const commentLatency = new Trend('comment_post_latency_ms', true);
const successCount = new Counter('success_count');
const failCount = new Counter('fail_count');

// ── Load profile ──────────────────────────────────────────────────────────────
export const options = {
  stages: [
    { duration: '30s', target: 10 },   // ramp-up
    { duration: '60s', target: 50 },   // sustained load - 50 concurrent users
    { duration: '30s', target: 50 },   // hold
    { duration: '20s', target: 0 },    // ramp-down
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000'],  // 95% of requests under 3s
    error_rate: ['rate<0.05'],          // error rate under 5%
  },
};

// ── Setup: register a test user and get token ─────────────────────────────────
export function setup() {
  const username = `loadtest_${Date.now()}`;
  const payload = JSON.stringify({
    user: {
      username,
      email: `${username}@loadtest.local`,
      password: 'LoadTest123!',
    },
  });

  const res = http.post(`${API_URL}/users`, payload, {
    headers: { 'Content-Type': 'application/json' },
  });

  if (res.status !== 200 && res.status !== 201) {
    console.error(`Setup failed: ${res.status} ${res.body}`);
    return { token: null, articleSlug: null };
  }

  const token = res.json('user.token');

  // Create a test article
  const articlePayload = JSON.stringify({
    article: {
      title: `Load Test Article ${Date.now()}`,
      description: 'Article for load testing comment submission',
      body: 'This article is used to benchmark the comment processing pipeline.',
      tagList: ['loadtest'],
    },
  });

  const articleRes = http.post(`${API_URL}/articles`, articlePayload, {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Token ${token}`,
    },
  });

  const slug = articleRes.json('article.slug');
  console.log(`Setup complete. Token: ${token ? 'OK' : 'FAIL'}, Slug: ${slug}`);
  return { token, articleSlug: slug };
}

// ── Main VU loop ──────────────────────────────────────────────────────────────
export default function (data) {
  const { token, articleSlug } = data;

  if (!token || !articleSlug) {
    errorRate.add(1);
    failCount.add(1);
    return;
  }

  const commentBody = `Load test comment ${Date.now()} - testing sentiment analysis pipeline performance`;

  const start = Date.now();
  const res = http.post(
    `${API_URL}/articles/${articleSlug}/comments`,
    JSON.stringify({ comment: { body: commentBody } }),
    {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Token ${token}`,
      },
    }
  );
  const elapsed = Date.now() - start;

  const ok = check(res, {
    'status is 200 or 201': (r) => r.status === 200 || r.status === 201,
    'has comment id': (r) => r.json('comment.id') !== undefined,
    'status is pending': (r) => r.json('comment.status') === 'pending',
  });

  commentLatency.add(elapsed);
  errorRate.add(!ok ? 1 : 0);

  if (ok) {
    successCount.add(1);
  } else {
    failCount.add(1);
    console.warn(`POST comment failed: ${res.status} ${res.body.substring(0, 200)}`);
  }

  sleep(1);
}

// ── Teardown: print summary ───────────────────────────────────────────────────
export function teardown(data) {
  console.log('Load test complete.');
}
