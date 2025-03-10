#!/bin/bash

cp /Certs/test-ca.crt /usr/local/share/ca-certificates/test-ca.crt
chmod 644 /usr/local/share/ca-certificates/test-ca.crt && update-ca-certificates
touch /tmp/certready