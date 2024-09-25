# NSD

Cross-platform UI tool for estimating noise spectral density (NSD) from time-series data. 

This is intended to be an easy-to-use tool which allows anyone to quickly produce NSD charts that are directly comparable to other people's charts if they are produced by the same version of this tool. Customisation and configurability is very limited therefore power users will prefer their own scripts.

<img src="/docs/v1.4.png" height="500">

## Input file format

Input file format is single column CSV with no header.

Example:
```
-5.259715387375001E-07
-4.895393397810999E-07
-5.413877378806E-07
-5.731182876255E-07
-5.228528194452E-07
```

Multiple number formats supported, however not exhaustive.

## Using release binaries

### Windows

It may need unblocking on properties dialog

![image](https://user-images.githubusercontent.com/1031306/184110097-253fddf3-4037-48ab-867a-c62fa61b87c4.png)

### Linux

```
sudo apt-get install -y libgdiplus
chmod +x NSD.linux-x64
```
