# Fetchit.Listenerd

## Quick Deployment

Deploy Fetchit.Listenerd with a single command:

```bash
sudo bash -c "$(curl -fsSL https://raw.githubusercontent.com/SchultzTechnology/Fetchit.Listenerd/main/deploy.sh)"
```

Or using wget:

```bash
sudo bash -c "$(wget -qO- https://raw.githubusercontent.com/SchultzTechnology/Fetchit.Listenerd/main/deploy.sh)"
```

This will:
- Clone the repository to `/opt/fetchit`
- Install all dependencies (.NET 9.0, libpcap, supervisor)
- Build and publish both the listener service and web application
- Configure and start the services via Supervisor
- Set up firewall rules

After deployment, access the web interface at `http://localhost:8080`

## Requirements

- Ubuntu/Debian-based Linux system
- Root access (sudo)
- Internet connection
