# GModGaurd
A simple [source server query](https://developer.valvesoftware.com/wiki/Server_queries) cache for Garry's Mod. This has not been tested on other source games.

## How to
- Install dotnet:
https://www.microsoft.com/net/core#linuxdebian

- Edit and move Servers.json to your install dir

- Edit the user and directories in gmodgaurd.service
- Move gmodgaurd.service to /etc/systemd/system (if your on a system that doesnt have systemctl you'll be on your own as far as auto restaring goes)

- Enable & Start it:
systemctl daemon-reload
systemctl disable gmodgaurd.service
systemctl restart gmodgaurd
systemctl stop gmodgaurd

- Add iptables rules in FIREWALL RULES.txt (May also want to rate limit a2s_* to start I have no idea how much cpu this will use if someone decides to try hard).
