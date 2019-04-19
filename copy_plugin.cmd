if not exist "%userprofile%\Documents\QSC\Q-Sys Designer\Plugins\%1" mkdir "%userprofile%\Documents\QSC\Q-Sys Designer\Plugins\%1"

TYPE "C:\plugins-layout-test\%1\%1.qplug" > "%userprofile%\Documents\QSC\Q-Sys Designer\Plugins\%1\%1.qplug"