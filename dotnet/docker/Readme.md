

[crystal report downloads](https://origin.softwaredownloads.sap.com/public/site/index.html)

- crystal 13SP39
    - https://origin-az.softwaredownloads.sap.com/public/file/0025000000164892025 
    - sha256:0716944644faf8e7c2613bd46655c79454808a2a3dd71e58642939fd7839320c
- crystal 13SP38
    - https://origin-az.softwaredownloads.sap.com/public/file/0020000000551772025
    - sha256:2f843563e1ffcb87216e7e0d1d27702b8127fd7294bcee398b9e0aa10c3412a4
- crystal 13SP37
    - https://origin-az.softwaredownloads.sap.com/public/file/0020000001375542024
    - sha256:74ed30006679aa82300468e3e58cfe014fb663e257bb35d9d3046a6e7791f4fa

build and run
```bash
docker compose up --build -d
```

run

```bash
docker compose up
```

build oci and push to dockerhub
```bash
./build.sh
```