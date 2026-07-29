#!/bin/bash

export AWS_REGION=eu-west-2
export AWS_DEFAULT_REGION=eu-west-2
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

set -e

# S3 buckets
echo "Bootstrapping S3 setup..."

# add buckets here

echo "Bootstrapping SQS setup..."

# Create SQS resources
queue_url=$(awslocal sqs create-queue  \
  --queue-name identity_service_helper_intake \
  --endpoint-url=http://localhost:4566 \
  --output text \
  --query 'QueueUrl')

echo "SQS Queue created: $queue_url"

# Get the SQS Queue ARN
queue_arn=$(awslocal sqs get-queue-attributes \
  --queue-url "$queue_url" \
  --attribute-name QueueArn \
  --output text \
  --query 'Attributes.QueueArn')

echo "SQS Queue ARN: $queue_arn"

# Create SNS Topics
topic_arn=$(awslocal sns create-topic \
  --name ls_keeper_data_import_complete \
  --endpoint-url=http://localhost:4566 \
  --output text \
  --query 'TopicArn')

echo "SNS Topic created: $topic_arn"

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "jq is not installed. Installing jq..."
    apk add --no-cache jq 2>/dev/null || apt-get update && apt-get install -y jq 2>/dev/null || yum install -y jq
fi

# Construct the policy JSON inline with escaped quotes
policy_json=$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": "*",
      "Action": "sqs:SendMessage",
      "Resource": "$queue_arn",
      "Condition": {
        "ArnEquals": {
          "aws:SourceArn": "$topic_arn"
        }
      }
    }
  ]
}
EOF
)

# Set SQS policy - escape the JSON properly
policy_escaped=$(echo "$policy_json" | jq -c | sed 's/"/\\"/g')
awslocal sqs set-queue-attributes \
  --queue-url "$queue_url" \
  --attributes "{\"Policy\": \"$policy_escaped\"}"

# Subscribe the Queue to the Topic
awslocal sns subscribe \
  --topic-arn "$topic_arn" \
  --protocol sqs \
  --notification-endpoint "$queue_arn" \
  --endpoint-url http://localhost:4566

echo "SNS Topic subscription complete"

echo "Bootstrapping Complete"
