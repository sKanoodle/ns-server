#!/bin/bash
SERVER_NAME=''
SERVER_PORT='24654'
PW=''
IP=$( curl -s 'ipinfo.io' | jq -r '.ip' )
(echo -e "login: ${PW}\n\nchange-ip: ${IP}\n"; sleep 5) | openssl s_client -connect $SERVER_NAME:$SERVER_PORT