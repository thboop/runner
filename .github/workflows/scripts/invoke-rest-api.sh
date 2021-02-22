#!/usr/bin/env bash

set -e

# Expected HTTP code or HTTP code file
if [ -z "$1" ]; then
  echo "Missing expected-HTTP-code argument"
  exit 1
fi
expected_http_code="$1"
http_code_file="$1"
case $string in
    ''|*[!0-9]*) http_code_file="$1" ;;
    *) expected_http_code="$1" ;;
esac

# Response body file
if [ -z "$2" ]; then
  echo "Missing response-body-file argument"
  exit 1
fi
response_body_file="$2"

# HTTP method
if [ -z "$3" ]; then
  echo "Missing HTTP-method argument"
  exit 1
fi
http_method="$3"

# URL
if [ -z "$4" ]; then
  echo "Missing URL argument"
  exit 1
fi
url="$4"

# Body
body="$5"

# Content type
content_type="$6"
if [ -z "$content_type" ]; then
  content_type='application/json'
fi

# Delete any previous response-body-file
rm "$response_body_file" 1>/dev/null 2>&1 || :

# Echo the request method, URL, and body
echo "$http_method $url"
if [ -n "$body" ]; then
  echo "Body: $body"
fi

# Invoke the REST API
if [ -n "$ACCESS_TOKEN" ]; then
  auth="Authorization: bearer $ACCESS_TOKEN"
  code=$(curl --silent --show-error --write-out '%{http_code}' --output "$response_body_file" --header "$auth" --header "Content-Type: ${content_type}" --request "$http_method" --data "$body" "$url")
else
  code=$(curl --silent --show-error --write-out '%{http_code}' --output "$response_body_file" --header "Content-Type: ${content_type}" --request "$http_method" --data "$body" "$url")
fi

# Check the HTTP code
echo "HTTP code $code"
if [ -n "$http_code_file" ]; then
  echo -n "$code" > "$http_code_file"
elif [[ "$code" != "$expected_http_code" ]]; then
  cat "$response_body_file" || :
  exit 1
fi
