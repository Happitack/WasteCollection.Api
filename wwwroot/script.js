document.addEventListener('DOMContentLoaded', () => {

    const requestForm = document.getElementById('request-form');
    const descriptionInput = document.getElementById('description');
    const wasteTypeInput = document.getElementById('waste-type');
    const contactInfoInput = document.getElementById('contact-info');
    const statusMessageDiv = document.getElementById('status-message');
    const requestsListDiv = document.getElementById('requests-list');

    const apiUrl = '/api/Requests';

    function showStatusMessage(message, isSuccess) {
        statusMessageDiv.textContent = message;
        statusMessageDiv.className = isSuccess ? 'status-success' : 'status-error';
        setTimeout(() => {
            statusMessageDiv.textContent = '';
            statusMessageDiv.className = '';
        }, 5000); 
    }

    async function handleFormSubmit(event) {
        event.preventDefault();

        statusMessageDiv.textContent = '';
        statusMessageDiv.className = '';

        const description = descriptionInput.value.trim();
        const wasteType = wasteTypeInput.value.trim();
        const contactInfo = contactInfoInput.value.trim(); 

        if (!description || !wasteType) {
            showStatusMessage('Description and Waste Type are required.', false);
            return;
        }

        const requestData = {
            Description: description, 
            WasteType: wasteType,
            ContactInfo: contactInfo || null 
        };

        try {
            const response = await fetch(apiUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json' 
                },
                body: JSON.stringify(requestData) 
            });

            if (response.ok) {
                showStatusMessage('Request submitted successfully!', true);
                requestForm.reset();
                await fetchAndDisplayRequests();
            } else {
                let errorMessage = `Error: ${response.status} ${response.statusText}`;
                try {
                    const errorData = await response.json();
                    if (errorData && errorData.title) {
                        errorMessage = errorData.title;
                    } else if (typeof errorData === 'string') {
                         errorMessage = errorData;
                    }
                } catch (parseError) {
                }
                showStatusMessage(errorMessage, false);
            }
        } catch (error) {
            console.error('Network error:', error);
            showStatusMessage('Network error. Could not submit request.', false);
        }
    }

    async function handleMarkComplete(requestId) {
        console.log(`Attempting to mark request ${requestId} as complete.`); 
        statusMessageDiv.textContent = ''; 
        statusMessageDiv.className = '';

        const completeUrl = `${apiUrl}/${requestId}/complete`;

        try {
            const response = await fetch(completeUrl, {
                method: 'PUT', 
                headers: {
                    'Accept': 'application/json' 
                }
            });

            if (response.ok) { 
                showStatusMessage(`Request ${requestId} marked as complete!`, true);
                await fetchAndDisplayRequests(); 
            } else {
                let errorMessage = `Error: ${response.status} ${response.statusText}`;
                try {
                    const errorData = await response.json();
                     if (errorData && (errorData.title || typeof errorData === 'string')) {
                        errorMessage = errorData.title || errorData;
                    } else if (typeof errorData === 'object' && errorData.detail) {
                         errorMessage = errorData.detail;
                    }
                } catch (parseError) { /* Ignore if no JSON body */ }
                showStatusMessage(errorMessage, false);
                 console.error(`Failed to mark request ${requestId} complete:`, errorMessage); 
            }
        } catch (error) {
            console.error('Network error marking complete:', error);
            showStatusMessage('Network error. Could not mark request as complete.', false);
        }
    }

    async function fetchAndDisplayRequests() {
        requestsListDiv.innerHTML = '<p>Loading requests...</p>';

        try {
            const response = await fetch(apiUrl);

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const requests = await response.json();

            if (requests.length === 0) {
                requestsListDiv.innerHTML = '<p>No requests submitted yet.</p>';
                return;
            }

            let tableHtml = `
                <table>
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Description</th>
                            <th>Waste Type</th>
                            <th>Status</th>
                            <th>Submitted At</th>
                            <th>Processed At</th>
                            <th>Action</th>
                        </tr>
                    </thead>
                    <tbody>
            `;

            const statusMap = { 0: 'Pending', 1: 'Processing', 2: 'Completed', 3: 'Cancelled' };

            requests.forEach(req => {
                const submittedDate = new Date(req.submittedAt).toLocaleString();
                const processedDate = req.processedAt ? new Date(req.processedAt).toLocaleString() : 'N/A';
                const statusText = statusMap[req.status] || 'Unknown';

                const isCompletable = req.status === 0 || req.status === 1;
                const buttonHtml = isCompletable
                    ? `<button class="complete-button" data-id="${req.id}">Mark Complete</button>`
                    : ''; // No button if already completed/cancelled

                tableHtml += `
                    <tr>
                        <td>${req.id}</td>
                        <td>${escapeHtml(req.description)}</td>
                        <td>${escapeHtml(req.wasteType)}</td>
                        <td>${escapeHtml(statusText)}</td>
                        <td>${submittedDate}</td>
                        <td>${processedDate}</td>
                        <td>${buttonHtml}</td>
                    </tr>
                `;
            });

            tableHtml += `
                    </tbody>
                </table>
            `;

            requestsListDiv.innerHTML = tableHtml;

        } catch (error) {
            console.error('Error fetching requests:', error);
            requestsListDiv.innerHTML = '<p>Error loading requests.</p>';
        }
    }

    function escapeHtml(unsafe) {
        if (!unsafe) return '';
        return unsafe
             .replace(/&/g, "&amp;")
             .replace(/</g, "&lt;")
             .replace(/>/g, "&gt;")
             .replace(/"/g, "&quot;")
             .replace(/'/g, "&#039;");
     }


    requestForm.addEventListener('submit', handleFormSubmit);

    requestsListDiv.addEventListener('click', (event) => {
        if (event.target && event.target.classList.contains('complete-button')) {
            const requestId = event.target.getAttribute('data-id');
            if (requestId) {
                handleMarkComplete(parseInt(requestId, 10));
            }
        }
    });

    fetchAndDisplayRequests();
});
