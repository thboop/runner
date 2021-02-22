#!/usr/bin/env bash

set -e

#
# Copied from https://stackoverflow.com/questions/296536/how-to-urlencode-data-for-curl-command
#
string="${1}"
strlen=${#string}
encoded=""

for (( pos=0 ; pos<strlen ; pos++ )); do
  local c=${string:$pos:1}
  case "$c" in
    [-_.~a-zA-Z0-9] ) local o="${c}" ;;
    * )               printf -v o '%%%02x' "'$c"
  esac
  encoded+="${o}"
done
echo -n "${encoded}"
