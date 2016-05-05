#!/usr/bin/sh
rscript ./totable.r | gawk '{ gsub("eh", "ê"); gsub("u:", "ü"); gsub("ih", "ɨ"); gsub("a/", "a/阿"); gsub("e/", "e/饿"); gsub("mei/", "mei/美"); gsub("wo/", "wo/我"); gsub("wu/", "wu/五"); gsub("yi/", "yi/已"); gsub("yu/", "yu/雨"); gsub("ri/", "ri/日"); gsub("zi/", "zi/子"); print }'
