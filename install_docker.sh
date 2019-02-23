#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


ssh root@c.majorsilence.com '/snap/bin/docker run -p 4321:4321 -d --restart always majorsilence/crystalcmd'
