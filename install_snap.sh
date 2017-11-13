#!/usr/bin/env bash
set -e # exit on first error
set -u # exit on using unset variable


snap install ./Java/build/CrystalCmd.snap --force-dangerous --classic # force dangerous is needed because the snap is not signed
