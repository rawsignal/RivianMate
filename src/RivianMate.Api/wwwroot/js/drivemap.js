// Drive Map - Leaflet integration for RivianMate
window.driveMap = {
    map: null,
    routeLayer: null,
    markersLayer: null,

    // Initialize the map in the specified container
    initialize: function (containerId) {
        // Clean up existing map if any
        if (this.map) {
            this.map.remove();
            this.map = null;
        }

        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Map container not found:', containerId);
            return false;
        }

        // Create the map centered on a default location (will be updated when route loads)
        this.map = L.map(containerId, {
            zoomControl: true,
            attributionControl: true
        }).setView([37.7749, -122.4194], 13);

        // Add OpenStreetMap tiles
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(this.map);

        // Create layer groups for route and markers
        this.routeLayer = L.layerGroup().addTo(this.map);
        this.markersLayer = L.layerGroup().addTo(this.map);

        return true;
    },

    // Draw a route from an array of positions
    drawRoute: function (positions) {
        if (!this.map || !positions || positions.length === 0) {
            console.warn('Cannot draw route: map not initialized or no positions');
            return false;
        }

        // Clear existing route and markers
        this.routeLayer.clearLayers();
        this.markersLayer.clearLayers();

        // Extract coordinates [lat, lng] from positions
        const coordinates = positions.map(p => [p.latitude, p.longitude]);

        if (coordinates.length < 2) {
            console.warn('Not enough coordinates to draw route');
            return false;
        }

        // Draw the route polyline with a gradient effect based on speed
        // For simplicity, we'll use segments colored by speed
        if (positions[0].speed !== null && positions[0].speed !== undefined) {
            this.drawSpeedColoredRoute(positions);
        } else {
            // Simple single-color route
            const routeLine = L.polyline(coordinates, {
                color: '#4A90E2',
                weight: 4,
                opacity: 0.8,
                lineJoin: 'round'
            });
            this.routeLayer.addLayer(routeLine);
        }

        // Add start marker (green)
        const startPos = coordinates[0];
        const startMarker = L.marker(startPos, {
            icon: L.divIcon({
                className: 'drive-marker drive-marker-start',
                html: '<div class="marker-inner"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg></div>',
                iconSize: [32, 32],
                iconAnchor: [16, 16]
            })
        }).bindPopup('Start');
        this.markersLayer.addLayer(startMarker);

        // Add end marker (red)
        const endPos = coordinates[coordinates.length - 1];
        const endMarker = L.marker(endPos, {
            icon: L.divIcon({
                className: 'drive-marker drive-marker-end',
                html: '<div class="marker-inner"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg></div>',
                iconSize: [32, 32],
                iconAnchor: [16, 32]
            })
        }).bindPopup('End');
        this.markersLayer.addLayer(endMarker);

        // Fit the map to show the entire route with some padding
        const bounds = L.latLngBounds(coordinates);
        this.map.fitBounds(bounds, { padding: [30, 30] });

        return true;
    },

    // Draw route with colors based on speed
    drawSpeedColoredRoute: function (positions) {
        // Find min/max speed for color scaling
        const speeds = positions.filter(p => p.speed != null).map(p => p.speed);
        if (speeds.length === 0) {
            // Fallback to simple route
            const coordinates = positions.map(p => [p.latitude, p.longitude]);
            const routeLine = L.polyline(coordinates, {
                color: '#4A90E2',
                weight: 4,
                opacity: 0.8
            });
            this.routeLayer.addLayer(routeLine);
            return;
        }

        const minSpeed = Math.min(...speeds);
        const maxSpeed = Math.max(...speeds);
        const speedRange = maxSpeed - minSpeed || 1;

        // Draw segments with color based on speed
        for (let i = 0; i < positions.length - 1; i++) {
            const p1 = positions[i];
            const p2 = positions[i + 1];
            const speed = p1.speed || 0;

            // Normalize speed to 0-1 range
            const normalized = (speed - minSpeed) / speedRange;

            // Color gradient: green (slow) -> yellow -> red (fast)
            const color = this.speedToColor(normalized);

            const segment = L.polyline([
                [p1.latitude, p1.longitude],
                [p2.latitude, p2.longitude]
            ], {
                color: color,
                weight: 4,
                opacity: 0.8,
                lineJoin: 'round',
                lineCap: 'round'
            });

            this.routeLayer.addLayer(segment);
        }
    },

    // Convert normalized speed (0-1) to color
    speedToColor: function (normalized) {
        // Green (#22c55e) at 0, Yellow (#eab308) at 0.5, Red (#ef4444) at 1
        let r, g, b;

        if (normalized < 0.5) {
            // Green to Yellow
            const t = normalized * 2;
            r = Math.round(34 + (234 - 34) * t);
            g = Math.round(197 + (179 - 197) * t);
            b = Math.round(94 + (8 - 94) * t);
        } else {
            // Yellow to Red
            const t = (normalized - 0.5) * 2;
            r = Math.round(234 + (239 - 234) * t);
            g = Math.round(179 + (68 - 179) * t);
            b = Math.round(8 + (68 - 8) * t);
        }

        return `rgb(${r}, ${g}, ${b})`;
    },

    // Clear the route and markers
    clear: function () {
        if (this.routeLayer) {
            this.routeLayer.clearLayers();
        }
        if (this.markersLayer) {
            this.markersLayer.clearLayers();
        }
    },

    // Destroy the map
    dispose: function () {
        if (this.map) {
            this.map.remove();
            this.map = null;
            this.routeLayer = null;
            this.markersLayer = null;
        }
    },

    // Invalidate size (call after container becomes visible)
    invalidateSize: function () {
        if (this.map) {
            this.map.invalidateSize();
        }
    }
};
