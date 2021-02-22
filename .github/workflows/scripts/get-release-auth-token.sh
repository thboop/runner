#!/usr/bin/env bash

set -e

# Dot source common functions
script_dir="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

# Start time
start_time="$(date +%s)" # Seconds since Unix epoch
echo "Start time $(date -j -f '%s' $start_time)"

# Get a device code
tenant_id='72f988bf-86f1-41af-91ab-2d7cd011db47'
url="https://login.microsoftonline.com/${tenant_id}/oauth2/v2.0/devicecode"
client_id='15318e0c-80a4-4e6f-84e4-1ac4a983bbd8'
body="client_id=${client_id}&scope=499b84ac-1321-427f-aa17-267ca6975798/.default"
device_code_file='device-code-response.json'
"$script_dir/invoke-rest-api.sh" '200' "$device_code_file" 'POST' "$url" "$body" 'application/x-www-form-urlencoded'
# Response looks like:
#   {
#     user_code: string
#     device_code: string
#     verification_uri: string
#     expires_in: int
#     interval: int
#     message: string
#   }
# Use this command for testing locally:
#   curl --silent --show-error --write-out '%{http_code}' --output response.json --header 'Content-Type: application/x-www-form-urlencoded' --request POST --data 'client_id=15318e0c-80a4-4e6f-84e4-1ac4a983bbd8&scope=499b84ac-1321-427f-aa17-267ca6975798/.default' 'https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/v2.0/devicecode'
expires_in="$(cat "$device_code_file" | jq --raw-output '.expires_in')"
expires_at=$(($start_time + $expires_in))
interval="$(cat "$device_code_file" | jq --raw-output '.interval')"
device_code="$(cat "$device_code_file" | jq --raw-output '.device_code')"
message="$(cat "$device_code_file" | jq --raw-output '.message')"
rm "$device_code_file" 1>/dev/null 2>&1 || :
echo "Expires in ${expires_in}s at $(date -j -f '%s' $expires_at)"
echo "Polling interval ${interval}s"
echo "$message"

# Poll for a token
access_token=''
while [ $expires_at -gt "$(date +%s)" ]; do
  url="https://login.microsoftonline.com/${tenant_id}/oauth2/v2.0/token"
  body="client_id=${client_id}&grant_type=urn:ietf:params:oauth:grant-type:device_code&device_code=${device_code}"
  token_status_file='token-http-status'
  token_file='token-response.json'
  "$script_dir/invoke-rest-api.sh" "$token_status_file" "$token_file" 'POST' "$url" "$body" 'application/x-www-form-urlencoded' 1>/dev/null 2>&1
  # Successful response looks like:
  #   {
  #     token_type: "Bearer",
  #     scope: string,
  #     expires_in: int
  #     ext_expires_in: int
  #     access_token: string
  #   }
  http_status="$(cat "$token_status_file")"
  rm "$token_status_file" 1>/dev/null 2>&1 || :
  if [ "$http_status" = '200' ]; then
    access_token="$(cat "$token_file" | jq --raw-output '.access_token')"
    rm "$token_file" 1>/dev/null 2>&1 || :
    break
  fi

  # Error response looks like:
  #   {
  #     error: "authorization_pending",
  #     error_description: string,
  #     error_codes:[70016],
  #     timestamp: "2021-02-22 20:11:29Z",
  #     trace_id: string,
  #     correlation_id: string,
  #     error_uri: string
  #   }
  error_code="$(cat "$token_file" 2>/dev/null | jq --raw-output '.error' 2>/dev/null || :)"
  error_description="$(cat "$token_file" 2>/dev/null | jq --raw-output '.error_description' 2>/dev/null || :)"
  if [ "$error_code" != 'authorization_pending' ]; then
    echo "Failed polling for token. HTTP status $http_status"
    if [ -n "$error_code" ]; then
      echo "Error code: $error_code"
      echo "Error: $error_description"
      exit 1
    fi
  fi

  sleep "${interval}s"
done

if [ -z "$access_token" ]; then
  echo "Failed to retrieve a token before the polling expiration time"
  exit 1
fi
