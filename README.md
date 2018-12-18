EliasFanoCompression
====================

EliasFanoCompression: quasi-succinct compression of sorted integers in C#

Elias-Fano encoding is **quasi succinct**, which means it is **almost as good as the best theoretical possible compression scheme** for sorted integers. 
While it can be used to compress any sorted list of integers, we will use it for compressing posting lists of inverted indexes.
Based on a research paper by Sebastiano Vigna: http://vigna.di.unimi.it/ftp/papers/QuasiSuccinctIndices.pdf

#### Blog Post
[Elias-Fano: quasi-succinct compression of sorted integers in C# (2016)](https://medium.com/@wolfgarbe/elias-fano-quasi-succinct-compression-of-sorted-integers-in-c-89f92a8c9986)<br>

```
Copyright (C) 2016 Wolf Garbe
Version: 1.0
Author: Wolf Garbe <wolf.garbe@faroo.com>
Maintainer: Wolf Garbe <wolf.garbe@faroo.com>
URL: http://blog.faroo.com/2016/08/22/elias-fano_quasi-succinct_compression_of_sorted_integers_in_csharp
Description: http://blog.faroo.com/2016/08/22/elias-fano_quasi-succinct_compression_of_sorted_integers_in_csharp

License:
This program is free software; you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License, 
version 3.0 (LGPL-3.0) as published by the Free Software Foundation.
http://www.opensource.org/licenses/LGPL-3.0
```
