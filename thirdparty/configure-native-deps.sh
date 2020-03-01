#!/bin/sh
# Use BSD/Linux system dependencies where possible, but take into account different .so names.

####
# This file must stay /bin/sh and POSIX compliant for BSD portability.
# Copy-paste the entire script into http://shellcheck.net to check.
####

locations="/lib /lib64 /usr/lib /usr/lib64 /usr/lib/i386-linux-gnu /usr/lib/x86_64-linux-gnu /usr/lib/arm-linux-gnueabihf /usr/lib/aarch64-linux-gnu /usr/lib/powerpc64le-linux-gnu /usr/lib/mipsel-linux-gnu /usr/local/lib /opt/lib /opt/local/lib /app/lib"
sonames="liblua.so.5.1.5 liblua5.1.so.5.1 liblua5.1.so.0 liblua.so.5.1 liblua-5.1.so liblua5.1.so"

if [ -f Eluant.dll.config ]; then
	exit 0
fi

for location in $locations ; do
	for soname in $sonames ; do
		if [ -f "$location/$soname" ]; then
			liblua51=$soname
			echo "Detected Lua 5.1 library at $location/$soname"
			break 2
		fi
	done
done

if [ -z "$liblua51" ]; then
	echo "Lua 5.1 library detection failed."
	exit 1
else
	sed "s/lua51.so/${liblua51}/" Eluant.dll.config a> Eluant.dll.config.temp
	mv Eluant.dll.config.temp Eluant.dll.config

	echo "Eluant.dll.config has been created successfully."
fi

sed "s/SDL2.so/libSDL2-2.0.so.0/" SDL2-CS.dll.config > SDL2-CS.dll.config.temp
mv SDL2-CS.dll.config.temp SDL2-CS.dll.config

sed "s/soft_oal.so/libopenal.so.1/" OpenAL-CS.Core.dll.config > OpenAL-CS.Core.dll.config.temp
mv OpenAL-CS.Core.dll.config.temp OpenAL-CS.Core.dll.config


