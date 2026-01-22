// Touch-to-drag polyfill for mobile devices
// Translates touch events to work with Blazor's drag and drop

window.touchDrag = {
    // State
    activeElement: null,
    startX: 0,
    startY: 0,
    offsetX: 0,
    offsetY: 0,
    clone: null,
    scrollInterval: null,

    // Initialize touch handling for draggable elements
    initialize: function() {
        document.addEventListener('touchstart', this.handleTouchStart.bind(this), { passive: false });
        document.addEventListener('touchmove', this.handleTouchMove.bind(this), { passive: false });
        document.addEventListener('touchend', this.handleTouchEnd.bind(this), { passive: false });
        document.addEventListener('touchcancel', this.handleTouchEnd.bind(this), { passive: false });
    },

    handleTouchStart: function(e) {
        // Find the draggable element (dashboard-card-wrapper with draggable="true")
        const draggable = e.target.closest('.dashboard-card-wrapper[draggable="true"]');
        if (!draggable) return;

        // Check if touch started on the drag handle or card (in edit mode)
        const dragHandle = e.target.closest('.drag-handle');
        const isEditMode = draggable.classList.contains('edit-mode');

        // Only start drag if in edit mode
        if (!isEditMode) return;

        // Store touch info
        const touch = e.touches[0];
        this.activeElement = draggable;
        this.startX = touch.clientX;
        this.startY = touch.clientY;

        // Get element position for offset calculation
        const rect = draggable.getBoundingClientRect();
        this.offsetX = touch.clientX - rect.left;
        this.offsetY = touch.clientY - rect.top;

        // Add visual feedback after short delay (to distinguish from tap)
        this.longPressTimer = setTimeout(() => {
            if (this.activeElement) {
                this.startDrag(touch);
            }
        }, 150);
    },

    startDrag: function(touch) {
        if (!this.activeElement) return;

        // Create visual clone for dragging
        const rect = this.activeElement.getBoundingClientRect();
        this.clone = this.activeElement.cloneNode(true);
        this.clone.classList.add('touch-drag-clone');
        this.clone.style.cssText = `
            position: fixed;
            left: ${rect.left}px;
            top: ${rect.top}px;
            width: ${rect.width}px;
            height: ${rect.height}px;
            z-index: 10000;
            pointer-events: none;
            opacity: 0.85;
            transform: scale(1.02);
            box-shadow: 0 8px 32px rgba(0,0,0,0.3);
            transition: transform 0.1s ease, box-shadow 0.1s ease;
        `;
        document.body.appendChild(this.clone);

        // Mark original as dragging
        this.activeElement.classList.add('dragging');

        // Trigger Blazor dragstart
        this.triggerDragEvent(this.activeElement, 'dragstart');

        // Haptic feedback if available
        if (navigator.vibrate) {
            navigator.vibrate(10);
        }
    },

    handleTouchMove: function(e) {
        if (!this.activeElement) return;

        const touch = e.touches[0];
        const moveDistance = Math.sqrt(
            Math.pow(touch.clientX - this.startX, 2) +
            Math.pow(touch.clientY - this.startY, 2)
        );

        // If moved significantly before long press timer, cancel
        if (!this.clone && moveDistance > 10) {
            this.cancelDrag();
            return;
        }

        // If we haven't started dragging yet, wait for long press
        if (!this.clone) return;

        // Prevent scrolling while dragging
        e.preventDefault();

        // Move the clone
        this.clone.style.left = (touch.clientX - this.offsetX) + 'px';
        this.clone.style.top = (touch.clientY - this.offsetY) + 'px';

        // Find element under touch point (excluding clone)
        this.clone.style.display = 'none';
        const elementBelow = document.elementFromPoint(touch.clientX, touch.clientY);
        this.clone.style.display = '';

        if (elementBelow) {
            const dropTarget = elementBelow.closest('.dashboard-card-wrapper[draggable="true"]');
            if (dropTarget && dropTarget !== this.activeElement) {
                // Trigger dragenter on new target
                this.triggerDragEvent(dropTarget, 'dragenter');
            }
        }

        // Auto-scroll if near edges
        this.handleAutoScroll(touch.clientY);
    },

    handleTouchEnd: function(e) {
        clearTimeout(this.longPressTimer);
        this.stopAutoScroll();

        if (!this.activeElement) return;

        if (this.clone) {
            // Get final drop target
            const touch = e.changedTouches[0];
            this.clone.style.display = 'none';
            const elementBelow = document.elementFromPoint(touch.clientX, touch.clientY);
            this.clone.style.display = '';

            if (elementBelow) {
                const dropTarget = elementBelow.closest('.dashboard-card-wrapper[draggable="true"]');
                if (dropTarget) {
                    this.triggerDragEvent(dropTarget, 'drop');
                } else {
                    // Check if dropping on section container
                    const section = elementBelow.closest('.edit-mode-section');
                    if (section) {
                        this.triggerDragEvent(section, 'drop');
                    }
                }
            }

            // Trigger dragend
            this.triggerDragEvent(this.activeElement, 'dragend');

            // Clean up clone
            this.clone.remove();
            this.clone = null;
        }

        // Clean up
        if (this.activeElement) {
            this.activeElement.classList.remove('dragging');
        }
        this.activeElement = null;
    },

    cancelDrag: function() {
        clearTimeout(this.longPressTimer);
        this.stopAutoScroll();

        if (this.clone) {
            this.clone.remove();
            this.clone = null;
        }

        if (this.activeElement) {
            this.activeElement.classList.remove('dragging');
        }
        this.activeElement = null;
    },

    triggerDragEvent: function(element, eventType) {
        // Create and dispatch the event for Blazor to handle
        const event = new DragEvent(eventType, {
            bubbles: true,
            cancelable: true,
            dataTransfer: new DataTransfer()
        });
        element.dispatchEvent(event);
    },

    handleAutoScroll: function(clientY) {
        const threshold = 60;
        const scrollSpeed = 8;
        const viewportHeight = window.innerHeight;

        this.stopAutoScroll();

        if (clientY < threshold) {
            // Scroll up
            this.scrollInterval = setInterval(() => {
                window.scrollBy(0, -scrollSpeed);
            }, 16);
        } else if (clientY > viewportHeight - threshold) {
            // Scroll down
            this.scrollInterval = setInterval(() => {
                window.scrollBy(0, scrollSpeed);
            }, 16);
        }
    },

    stopAutoScroll: function() {
        if (this.scrollInterval) {
            clearInterval(this.scrollInterval);
            this.scrollInterval = null;
        }
    }
};

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => touchDrag.initialize());
} else {
    touchDrag.initialize();
}
