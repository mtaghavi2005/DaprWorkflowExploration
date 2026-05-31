Feature: Order workflow processing

The order workflow should run through the real Dapr sidecars, state store,
pub/sub component, API service, and accounting service.

Scenario: Paid order completes and decrements inventory
    Given the following store exists
        | Id        | Name      | Description               | Price | Quantity |
        | bdd-store | BDD Store | Store seeded from a test. | 25.00 |        5 |
    When I post JSON to "/order/process" and capture workflow instance id from "workflowInstanceId"
        """
        {
          "storeId": "bdd-store",
          "quantity": 2
        }
        """
    Then the JSON response from "/order/process/{{workflowInstanceId}}" should contain within 120 seconds
        """
        {
          "runtimeStatus": "Completed",
          "isCompleted": true,
          "processed": true
        }
        """
    And workflow activity history should contain in order within 60 seconds
        | Name                    |
        | NotifyActivity          |
        | VerifyInventoryActivity |
        | ProcessPaymentActivity  |
        | UpdateInventoryActivity |
        | NotifyActivity          |
    And store "bdd-store" should have 3 items remaining