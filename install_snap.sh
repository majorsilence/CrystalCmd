#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


snap install ./java/CrystalCmd/build/CrystalCmd.snap --force-dangerous --classic # force dangerous is needed because the snap is not signed

scp ./java/CrystalCmd/build/CrystalCmd.snap root@c.majorsilence.com:/root
ssh root@c.majorsilence.com 'snap install /root/CrystalCmd.snap --force-dangerous --classic'
