#!/bin/bash

mkdir /tmp/test-ca

openssl genrsa -out /tmp/test-ca/test-ca.private-key.pem 4096
openssl rsa -in /tmp/test-ca/test-ca.private-key.pem -pubout -out /tmp/test-ca/test-ca.public-key.pem
openssl req -new -x509 -key /tmp/test-ca/test-ca.private-key.pem -out /tmp/test-ca/test-ca.cert.pem -days 365 -config test-ca.cnf
openssl x509 -in /tmp/test-ca/test-ca.cert.pem -out test-ca.crt

openssl genrsa -out /tmp/test-ca/localhost.private-key.pem 4096
openssl rsa -in /tmp/test-ca/localhost.private-key.pem -pubout -out /tmp/test-ca/localhost.public-key.pem
openssl req -new -sha256 -key /tmp/test-ca/localhost.private-key.pem -out /tmp/test-ca/localhost.csr -config localhost.cnf
openssl x509 -req -in /tmp/test-ca/localhost.csr -CA /tmp/test-ca/test-ca.cert.pem -CAkey /tmp/test-ca/test-ca.private-key.pem -CAcreateserial -out /tmp/test-ca/localhost.cer -days 365 -sha256 -extfile localhost.cnf -extensions req_v3
openssl pkcs12 -export -inkey /tmp/test-ca/localhost.private-key.pem -in /tmp/test-ca/localhost.cer -out localhost.pfx -passout file:Password.txt
