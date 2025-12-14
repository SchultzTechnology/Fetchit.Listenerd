#!/bin/bash

echo "=========================================="
echo "  Fetchit MQTT Configuration Manager"
echo "=========================================="
echo ""

# Check if docker and docker-compose are installed
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "Error: Docker Compose is not installed."
    exit 1
fi

# Create data directory if it doesn't exist
if [ ! -d "./data" ]; then
    echo "Creating data directory..."
    mkdir -p ./data
fi

echo "Building and starting services..."
docker-compose up -d --build

echo ""
echo "=========================================="
echo "Services started successfully!"
echo "=========================================="
echo ""
echo "Container: fetchit-combined (Running both services)"
echo "  - Web Application: http://localhost:8080"
echo "  - Listenerd Service: Running in background"
echo ""
echo "Useful commands:"
echo "  View logs:              docker-compose logs -f"
echo "  Check process status:   docker exec fetchit-combined supervisorctl status"
echo "  Stop services:          docker-compose down"
echo "  Restart services:       docker-compose restart"
echo ""
echo "View individual service logs:"
echo "  Listenerd:  docker exec fetchit-combined tail -f /var/log/supervisor/listenerd.out.log"
echo "  WebPage:    docker exec fetchit-combined tail -f /var/log/supervisor/webpage.out.log"
echo ""
echo "Database location: ./data/mqttconfig.db"
echo "=========================================="
