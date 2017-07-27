## uWebshop Version 3


### Hvað er það sem við viljum ná fram með þessari útgáfu ?
-  1. Virkar!
-  2. Hröð
-  3. Einfalari
-  4. Öruggari
-  6. Virki með 100 vörur en líka 100.000 vörur.

### Hvernig ætlum við að ná því fram ?
-  1. Betra og einfaldara Cache.
-  2. Ekki búa til óþarfa nóður inn í Umbraco eins og Orders sem hægir á kerfinu.
-  3. Afslættir ættu að virka betur, eru með vandræði í dag.
-  4. Búa til sér section inn í Umbraco til að halda utan um pantanir og reporting.
-  5. Betri og hraðari leið til að setja upp vörur/varianta
-  6. Hægt að endurgreiða stakar vörur

### Hvað er búið að gera ?
-  1. Öll helsta Catalog virknin er tilbúin til þess að birta vörur.
-  2. Cache/Api core virknin er tilbúin
-  3. Url provider er tilbúinn

### Hvað á eftir að gera ?
-  1. Körfu virkni
-  2. Pantana virkni
-  3. Afsláttar virkni
-  4. Greiðsluvirkni (Payment providers)
-  5. Sendingarmöguleikar
-  6. Setja upp eventa til þess að hooka sig inn í fyrir og eftir öll actions.

### Hvað þarf að athuga ?
-  1. Það þarf að vera hægt að stilla körfuna þannig að hægt sé að deila henni með fleiri búðum/lénum.


## uWebshop request in surface controllers / api controllers

We register an IHttpModule to ensure creation of an uwbsRequest entry in the runtimeCache.

The module listens for incoming requests containing a store querystring parameter.

This means controller actions do not need to ask for a store param as the module will read it from the request uri

and create the uwbsRequest object with store domainprefix and currency if applicable.
