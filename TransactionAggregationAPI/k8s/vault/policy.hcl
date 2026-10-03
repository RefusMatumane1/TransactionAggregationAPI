# Least privilege: each workload reads only its own secrets.json document.
path "secret/data/transaction-aggregation/api" {
  capabilities = ["read"]
}

path "secret/data/transaction-aggregation/worker" {
  capabilities = ["read"]
}
