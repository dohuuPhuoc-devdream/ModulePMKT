(function () {
 "use strict";
 var nav = document.querySelector(".main-navigation");
 var modules = Array.prototype.slice.call(document.querySelectorAll(".module > details"));
 modules.forEach(function (item) {
  item.addEventListener("toggle", function () {
   if (item.open) modules.forEach(function (other) { if (other !== item) other.open = false; });
  });
 });
 document.addEventListener("click", function (event) {
  if (!nav.contains(event.target)) modules.forEach(function (item) { item.open = false; });
 });
 nav.addEventListener("keydown", function (event) {
  if (event.key === "Escape") {
   var detail = event.target.closest("details");
   if (detail && detail.open) { detail.open = false; detail.querySelector("summary").focus(); event.preventDefault(); }
  }
 });
}());

