
setTimeout(function() {

    var sectionContainers = document.querySelectorAll(".sections li");

    for (var i = 0; i < sectionContainers.length; i++) {
      var sectionContainer = sectionContainers[i];

      if (sectionContainer !== null) {
        var dataElement = sectionContainer.getAttribute("data-element");

        if (dataElement === "section-ekommanager") {
          var aElement = sectionContainer.querySelector("a");
          aElement.href = "/umbraco/backoffice/ekom/manager";

          aElement.addEventListener("click", () => {
            //window.open('/umbraco/backoffice/ekom/manager', '_blank');
            window.location.href = "/umbraco/backoffice/ekom/manager";
          });
        }
      }
    }

}, 1000);


