setwd("C:/Users/Acshi/OneDrive/Documents/Visual Studio Projects/AccentTutor/mandarinsounds/collectedvowels")
library(lisp)

vowelToEntry <- function(header, v, topAmp, na.rm = TRUE) {
  # remove data points that are too far away from the median
  f1s <- v$f1
  f1s <- f1s[f1s > median(v$f1) - sd(v$f1)]
  f1s <- f1s[f1s < median(v$f1) + sd(v$f1)]
  
  f2s <- v$f2
  f2s <- f2s[f2s > median(v$f2) - sd(v$f2)]
  f2s <- f2s[f2s < median(v$f2) + sd(v$f2)]
  
  f3s <- v$f3
  f3s <- f3s[f3s > median(v$f3) - sd(v$f3)]
  f3s <- f3s[f3s < median(v$f3) + sd(v$f3)]
  
  f1q <- quantile(f1s)
  f2q <- quantile(f2s)
  f3q <- quantile(f3s)
  
  # Allow no more than +-maxSpread from the "central value", but make sure median is included
  maxSpread <- 300
  
  f1center<- (f1q[[2]] + f1q[[4]]) / 2
  f1min <- f1q[[1]]
  if (f1min < f1center - maxSpread) {
    f1min <- min(f1q[[3]], max(f1q[[2]], f1center - maxSpread))
  }
  f1max <- f1q[[5]]
  if (f1max > f1center + maxSpread) {
    f1max <- max(f1q[[3]], min(f1q[[4]], f1center + maxSpread))
  }
  f1sd <- min(maxSpread, (f1max - f1min) / 2)
  f1min <- f1min + f1sd / 2
  f1max <- f1max - f1sd / 2
  f1sd <- max(30, f1sd)
  
  f2center<- (f2q[[2]] + f2q[[4]]) / 2
  f2min <- f2q[[1]]
  if (f2min < f2center - maxSpread) {
    f2min <- min(f2q[[3]], max(f2q[[2]], f2center - maxSpread))
  }
  f2max <- f2q[[5]]
  if (f2max > f2center + maxSpread) {
    f2max <- max(f2q[[3]], min(f2q[[4]], f2center + maxSpread))
  }
  f2sd <- min(maxSpread, (f2max - f2min) / 2)
  f2min <- f2min + f2sd / 2
  f2max <- f2max - f2sd / 2
  f2sd <- max(30, f2sd)
  
  f3center<- (f3q[[2]] + f3q[[4]]) / 2
  f3min <- f3q[[1]]
  if (f3min < f3center - maxSpread) {
    f3min <- min(f3q[[3]], max(f3q[[2]], f3center - maxSpread))
  }
  f3max <- f3q[[5]]
  if (f3max > f3center + maxSpread) {
    f3max <- max(f3q[[3]], min(f3q[[4]], f3center + maxSpread))
  }
  f3sd <- min(maxSpread, (f3max - f3min) / 2)
  f3min <- f3min + f3sd / 2
  f3max <- f3max - f3sd / 2
  f3sd <- max(30, f3sd)
  
  return (paste(c(
          "new Vowel(", header, ", ",
          "new float[] { ", as.integer(round(f1min)), ", ", as.integer(round(f2min)), ", ", as.integer(round(f3min)), " }, ",
          "new float[] { ", as.integer(round(f1max)), ", ", as.integer(round(f2max)), ", ", as.integer(round(f3max)), " }, ",
          "new int[] { ", as.integer(round(f1sd)), ", ", as.integer(round(f2sd)), ", ", as.integer(round(f3sd)), " }, ",
          "new int[] { ",
          as.integer(round(log(median(v$a1) / topAmp) / log(10) * 10)), ", ",
          as.integer(round(log(median(v$a2) / topAmp) / log(10) * 10)), ", ",
          as.integer(round(log(median(v$a3) / topAmp) / log(10) * 10)), " }), "
          ), collapse = ""))
}

files <- (Sys.glob("*.csv"))
vs <- lapply(files, read.csv)
a1max <- max(sapply(vs, function(v) { return (median(v$a1)) }))

vowelHeaders = c("\"a\", \"as in a/\"",
                 "\"e\", \"as in e/\"",
                 "\"eh\", \"as in mei/\"",
                 "\"o\", \"as in wo/\"",
                 "\"u\", \"as in wu/\"",
                 "\"i\", \"as in yi/\"",
                 "\"u:\", \"as in yu/\"",
                 "\"r\", \"as in ri/\"",
                 "\"ih\", \"as in zi/\""
                 )

vTable <- sapply(zip.list(vowelHeaders, vs), function(nameV) { return(vowelToEntry(nameV[[1]], nameV[[2]], a1max)) })

writeLines(vTable)

